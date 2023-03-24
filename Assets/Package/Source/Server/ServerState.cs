/*
* All or portions of this file Copyright (c) Amazon.com, Inc. or its affiliates or
* its licensors.
*
* For complete copyright and license terms please see the LICENSE at the root of this
* distribution (the "License"). All use of this software is governed by the License,
* or, if provided, by the license below or the license accompanying this file. Do not
* remove or modify any license notices. This file is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using Aws.GameLift.Server.Model;
using log4net;

namespace Aws.GameLift.Server {
    public sealed class ServerState : IAuxProxyMessageHandler {
        static readonly double HEALTHCHECK_TIMEOUT_SECONDS = 60;

        HttpClientInvoker httpClientInvoker = new HttpClientInvoker();
        WebSocketListener webSocketListener;

        ProcessParameters processParameters;
        volatile bool processReady = false;
        string gameSessionId;
        DateTime terminationTime = DateTime.MinValue; //init to 1/1/0001 12:00:00 AM

        public static ServerState Instance { get; } = new ServerState();

        public static ILog Log { get; } = LogManager.GetLogger(typeof(ServerState));

        public GenericOutcome ProcessReady(ProcessParameters procParameters) {
            processReady = true;
            processParameters = procParameters;

            GenericOutcome result = httpClientInvoker.ProcessReady(procParameters.Port, procParameters.LogParameters.LogPaths).Result;

            Task.Run(() => StartHealthCheck());

            return result;
        }

        public GenericOutcome ProcessEnding() {
            processReady = false;
            return httpClientInvoker.ProcessEnding().Result;
        }

        public GenericOutcome ActivateGameSession() {
            if (String.IsNullOrEmpty(gameSessionId)) {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }
            return httpClientInvoker.ActivateGameSession(gameSessionId).Result;
        }

        public GenericOutcome TerminateGameSession() {
            if (String.IsNullOrEmpty(gameSessionId)) {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }
            return httpClientInvoker.TerminateGameSession(gameSessionId).Result;
        }

        public AwsStringOutcome GetGameSessionId() {
            if (String.IsNullOrEmpty(gameSessionId)) {
                return new AwsStringOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }
            return new AwsStringOutcome(gameSessionId);
        }

        public AwsDateTimeOutcome GetTerminationTime() {
            if (DateTime.MinValue == terminationTime) {
                return new AwsDateTimeOutcome(new GameLiftError(GameLiftErrorType.TERMINATION_TIME_NOT_SET));
            }
            return new AwsDateTimeOutcome(terminationTime);
        }

        public GenericOutcome UpdatePlayerSessionCreationPolicy(PlayerSessionCreationPolicy playerSessionPolicy) {
            if (String.IsNullOrEmpty(gameSessionId)) {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }
            return httpClientInvoker.UpdatePlayerSessionCreationPolicy(gameSessionId, playerSessionPolicy).Result;
        }

        public GenericOutcome AcceptPlayerSession(string playerSessionId) {
            if (String.IsNullOrEmpty(gameSessionId)) {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }
            return httpClientInvoker.AcceptPlayerSession(playerSessionId, gameSessionId).Result;
        }

        public GenericOutcome RemovePlayerSession(string playerSessionId) {
            if (String.IsNullOrEmpty(gameSessionId)) {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.GAMESESSION_ID_NOT_SET));
            }
            return httpClientInvoker.RemovePlayerSession(playerSessionId, gameSessionId).Result;
        }

        public DescribePlayerSessionsOutcome DescribePlayerSessions(DescribePlayerSessionsRequest request) {
            return httpClientInvoker.DescribePlayerSessions(request).Result;
        }

        public StartMatchBackfillOutcome BackfillMatchmaking(StartMatchBackfillRequest request) {
            return httpClientInvoker.BackfillMatchmaking(request).Result;
        }

        public GenericOutcome StopMatchmaking(StopMatchBackfillRequest request) {
            return httpClientInvoker.StopMatchmaking(request).Result;
        }

        void StartHealthCheck() {
            Log.Debug("HealthCheck thread started.");
            while (processReady) {
                Task.Run(() => ReportHealth());
                Thread.Sleep(TimeSpan.FromSeconds(HEALTHCHECK_TIMEOUT_SECONDS));
            }
        }

        void ReportHealth() {
            // duplicate ProcessReady check here right before invoking
            if (!processReady) {
                Log.Debug("Reporting Health on an inactive process. Ignoring.");
                return;
            }

            Log.Debug("Reporting health using the OnHealthCheck callback.");
            IAsyncResult result = processParameters.OnHealthCheck.BeginInvoke(null, null);

            GenericOutcome outcome;
            if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(HEALTHCHECK_TIMEOUT_SECONDS))) {
                Log.Debug("Timed out waiting for health response from the server process. Reporting as unhealthy.");
                outcome = httpClientInvoker.ReportHealth(false).Result;
            } else {
                bool healthCheckResult = processParameters.OnHealthCheck.EndInvoke(result);

                Log.DebugFormat("Received health response from the server process: {0}", healthCheckResult);
                outcome = httpClientInvoker.ReportHealth(healthCheckResult).Result;
            }

            if (!outcome.Success) {
                Log.Warn("Could not send health status");
            }
        }

        public GenericOutcome InitializeNetworking() {
            webSocketListener = new WebSocketListener(this);
            return webSocketListener.Connect().Success ? new GenericOutcome() : new GenericOutcome(new GameLiftError(GameLiftErrorType.LOCAL_CONNECTION_FAILED));
        }

        public GetInstanceCertificateOutcome GetInstanceCertificate() {
            Log.DebugFormat("Calling GetInstanceCertificate");
            return httpClientInvoker.GetInstanceCertificate().Result;
        }

        public void OnStartGameSession(GameSession gameSession) {
            Log.DebugFormat("ServerState got the startGameSession signal. GameSession : {0}", gameSession);

            if (!processReady) {
                Log.Debug("Got a game session on inactive process. Ignoring.");
                return;
            }
            gameSessionId = gameSession.GameSessionId;

            Task.Run(() => {
                processParameters.OnStartGameSession(gameSession);
            });
        }

        public void OnUpdateGameSession(GameSession gameSession, UpdateReason updateReason, string backfillTicketId) {
            Log.DebugFormat("ServerState got the updateGameSession signal. GameSession : {0}", gameSession);

            if (!processReady) {
                Log.Warn("Got an updated game session on inactive process.");
                return;
            }

            Task.Run(() => {
                processParameters.OnUpdateGameSession(new UpdateGameSession(gameSession, updateReason, backfillTicketId));
            });
        }

        public void OnTerminateProcess(long terminationTime) {
            // TerminationTime coming from AuxProxy is milliseconds that have elapsed since Unix epoch time begins (00:00:00 UTC Jan 1 1970).
            this.terminationTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(terminationTime);

            Log.DebugFormat("ServerState got the terminateProcess signal. termination time : {0}", this.terminationTime);

            Task.Run(() => {
                processParameters.OnProcessTerminate();
            });
        }

        public void Shutdown() {
            this.processReady = false;

            //Sleep thread for 1 sec.
            //This is to help deal with race conditions related to processReady flag being turned off (i.e. ReportHealth)
            Thread.Sleep(1000);

            webSocketListener.Disconnect();
        }
    }
}
