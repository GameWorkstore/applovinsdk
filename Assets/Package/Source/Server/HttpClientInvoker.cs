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

using Aws.GameLift.Server.Model;
using Com.Amazon.Whitewater.Auxproxy.Pbuffer;
using Google.Protobuf;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Aws.GameLift.Server {
    public class HttpClientInvoker {
        static readonly string GAMELIFT_API_HEADER = "gamelift-target";
        static readonly string GAMELIFT_PID_HEADER = "gamelift-server-pid";

        static readonly ILog log = LogManager.GetLogger(typeof(HttpClientInvoker));

        HttpClient httpClient = new HttpClient();
        readonly string pid = Process.GetCurrentProcess().Id.ToString();

        public HttpClientInvoker() {
            httpClient.BaseAddress = new Uri("http://localhost:5758/");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add(GAMELIFT_PID_HEADER, pid);
        }

        public async Task<GenericOutcome> ProcessReady(int port, List<string> logPathsToUpload) {
            var pReady = new ProcessReady {
                Port = port,
                LogPathsToUpload = { logPathsToUpload }
            };

            var response = await SendAsync(pReady).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<GenericOutcome> ProcessEnding() {
            var response = await SendAsync(new ProcessEnding()).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<GenericOutcome> ReportHealth(bool isHealthy) {
            var rHealth = new ReportHealth {
                HealthStatus = isHealthy
            };

            var response = await SendAsync(rHealth).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<GenericOutcome> ActivateGameSession(string gameSessionId) {
            var gameSessionActivate = new GameSessionActivate {
                GameSessionId = gameSessionId
            };

            var response = await SendAsync(gameSessionActivate).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<GenericOutcome> TerminateGameSession(string gameSessionId) {
            var gameSessionTerminate = new GameSessionTerminate {
                GameSessionId = gameSessionId
            };

            var response = await SendAsync(gameSessionTerminate).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<GenericOutcome> UpdatePlayerSessionCreationPolicy(string gameSessionId, PlayerSessionCreationPolicy playerSessionPolicy) {
            var updatePlayerSessionCreationPolicy = new UpdatePlayerSessionCreationPolicy {
                GameSessionId = gameSessionId,
                NewPlayerSessionCreationPolicy = PlayerSessionCreationPolicyMapper.GetNameForPlayerSessionCreationPolicy(playerSessionPolicy)
            };

            var response = await SendAsync(updatePlayerSessionCreationPolicy).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<GenericOutcome> AcceptPlayerSession(string playerSessionId, string gameSessionId) {
            var acceptPlayerSession = new AcceptPlayerSession {
                PlayerSessionId = playerSessionId,
                GameSessionId = gameSessionId
            };

            var response = await SendAsync(acceptPlayerSession).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<GenericOutcome> RemovePlayerSession(string playerSessionId, string gameSessionId) {
            var removePlayerSession = new RemovePlayerSession {
                PlayerSessionId = playerSessionId,
                GameSessionId = gameSessionId
            };

            var response = await SendAsync(removePlayerSession).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<DescribePlayerSessionsOutcome> DescribePlayerSessions(Model.DescribePlayerSessionsRequest request) {
            var body = DescribePlayerSessionsRequestMapper.ParseFromDescribePlayerSessionsRequest(request);
            var response = await SendAsync(body).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) {
                var deserialized = DescribePlayerSessionsResponse.Parser.ParseJson(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                var translation = DescribePlayerSessionsResult.ParseFromBufferedDescribePlayerSessionsResponse(deserialized);
                return new DescribePlayerSessionsOutcome(translation);
            }

            return new DescribePlayerSessionsOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION));
        }

        public async Task<StartMatchBackfillOutcome> BackfillMatchmaking(StartMatchBackfillRequest request) {
            var body = BackfillDataMapper.CreateBufferedBackfillMatchmakingRequest(request);

            var response = await SendAsync(body).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) {
                var deserialized = BackfillMatchmakingResponse.Parser.ParseJson(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                var translation = BackfillDataMapper.ParseFromBufferedBackfillMatchmakingResponse(deserialized);
                return new StartMatchBackfillOutcome(translation);
            }

            return new StartMatchBackfillOutcome(new GameLiftError(GameLiftErrorType.SERVICE_CALL_FAILED));
        }

        public async Task<GenericOutcome> StopMatchmaking(StopMatchBackfillRequest request) {
            var stopMatchmakingRequest = BackfillDataMapper.CreateBufferedStopMatchmakingRequest(request);

            var response = await SendAsync(stopMatchmakingRequest).ConfigureAwait(false);

            return ParseHttpResponse(response);
        }

        public async Task<GetInstanceCertificateOutcome> GetInstanceCertificate() {
            var response = await SendAsync(new GetInstanceCertificate()).ConfigureAwait(false);

            if (response.IsSuccessStatusCode) {
                var deserialized = GetInstanceCertificateResponse.Parser.ParseJson(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                var translation = GetInstanceCertificateResult.ParseFromBufferedGetInstanceCertificateResponse(deserialized);
                return new GetInstanceCertificateOutcome(translation);
            }
            return new GetInstanceCertificateOutcome(new GameLiftError(GameLiftErrorType.SERVICE_CALL_FAILED));
        }

        private async Task<HttpResponseMessage> SendAsync(IMessage body) {
            var httpRequest = new HttpRequestMessage {
                Method = HttpMethod.Post
            };
            httpRequest.Headers.Add(GAMELIFT_API_HEADER, body.Descriptor.Name);
            httpRequest.Content = new ByteArrayContent(body.ToByteArray());

            return await httpClient.SendAsync(httpRequest).ConfigureAwait(false);
        }

        private GenericOutcome ParseHttpResponse(HttpResponseMessage response) {
            if (response.IsSuccessStatusCode) {
                return new GenericOutcome();
            }
            if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError) {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.INTERNAL_SERVICE_EXCEPTION));
            }

            //We do not send specific errors from AuxProxy, just assuming bad requests here.
            return new GenericOutcome(new GameLiftError(GameLiftErrorType.BAD_REQUEST_EXCEPTION));
        }
    }
}
