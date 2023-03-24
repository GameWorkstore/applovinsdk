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
using log4net;
using System.Diagnostics;
using WebSocketSharp;
using Com.Amazon.Whitewater.Auxproxy.Pbuffer;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using System;

namespace Aws.GameLift.Server
{
    public class WebSocketListener
    {
        static readonly string HOSTNAME = "127.0.0.1";
        static readonly string PORT = "5759";
        static readonly string PID_KEY = "pID";
        static readonly string SDK_VERSION_KEY = "sdkVersion";
        static readonly string FLAVOR_KEY = "sdkLanguage";
        static readonly string FLAVOR = "CSharp";

        static readonly ILog log = LogManager.GetLogger(typeof(WebSocketListener));

        WebSocket socket;
        readonly IAuxProxyMessageHandler auxproxyHandler;

        //We pick 3 for recursion here because we do not have any protobuf messages with more than 3 embedded messages.
        readonly JsonParser parser = new JsonParser(new JsonParser.Settings(3, TypeRegistry.FromMessages(AuxProxyToSdkEnvelope.Descriptor)));

        public WebSocketListener(IAuxProxyMessageHandler auxproxyHandler)
        {
            this.auxproxyHandler = auxproxyHandler;
        }

        public GenericOutcome Connect()
        {
            if (!PerformConnect())
            {
                return new GenericOutcome(new GameLiftError(GameLiftErrorType.LOCAL_CONNECTION_FAILED));
            }
            return new GenericOutcome();
        }

        private bool PerformConnect()
        {
            socket = new WebSocket(CreateURI());
            socket.OnOpen += (sender, e) => {
                log.Info("Connected to local agent.");
            };
            socket.OnClose += (sender, e) =>
            {
                log.InfoFormat("Socket disconnected. Code is {0}. Reason is {1}", e.Code, e.Reason);
            };
            socket.OnError += (sender, e) =>
            {
                log.ErrorFormat("Error received from local agent. Error is {0}", e.Message);
            };
            socket.OnMessage += (sender, e) => {
                if (e.IsPing)
                {
                    log.Debug("Received ping from local agent.");
                    return;
                }
                if (e.IsText)
                {
                    try
                    {
                        var message = parser.Parse(e.Data as string, AuxProxyToSdkEnvelope.Descriptor) as AuxProxyToSdkEnvelope;

                        if (message.InnerMessage.Is(ActivateGameSession.Descriptor))
                        {
                            log.InfoFormat("Received ActivateGameSession from GameLift. Data is \n{0}", e.Data);
                            var activateGameSessionMessage = message.InnerMessage.Unpack<ActivateGameSession>();
                            var gameSession = Model.GameSession.ParseFromBufferedGameSession(activateGameSessionMessage.GameSession);
                            auxproxyHandler.OnStartGameSession(gameSession);
                            return;
                        }
                        if (message.InnerMessage.Is(UpdateGameSession.Descriptor))
                        {
                            log.InfoFormat("Received UpdateGameSession from GameLift. Data is \n{0}", e.Data);
                            var updateGameSessionMessage = message.InnerMessage.Unpack<UpdateGameSession>();
                            var gameSession = Model.GameSession.ParseFromBufferedGameSession(updateGameSessionMessage.GameSession);
                            var updateReason = Model.UpdateReasonMapper.GetUpdateReasonForName(updateGameSessionMessage.UpdateReason);
                            auxproxyHandler.OnUpdateGameSession(gameSession, updateReason, updateGameSessionMessage.BackfillTicketId);
                            return;
                        }
                        if (message.InnerMessage.Is(TerminateProcess.Descriptor))
                        {
                            log.InfoFormat("Received TerminateProcess from GameLift. Data is \n{0}", e.Data);
                            var terminateProcessMessage = message.InnerMessage.Unpack<TerminateProcess>();
                            auxproxyHandler.OnTerminateProcess(terminateProcessMessage.TerminationTime);
                            return;
                        }
                        log.ErrorFormat("Unknown message type received. Data is \n{0}", e.Data);
                    } catch (Exception ex)
                    {
                        log.Error($"could not parse message. Data is \n{e.Data}", ex);
                    }
                } 
                else
                {
                    log.WarnFormat("Unknown Data received. Data is \n{0}", e.Data);
                }
            };

            socket.Connect();
            return socket.IsAlive;
        }

        private string CreateURI()
        {
            var queryString = string.Format("{0}={1}&{2}={3}&{4}={5}",
                                                PID_KEY,
                                                Process.GetCurrentProcess().Id.ToString(),
                                                SDK_VERSION_KEY,
                                                GameLiftServerAPI.GetSdkVersion().Result,
                                                FLAVOR_KEY,
                                                FLAVOR
                                               );
            var endpoint = string.Format("ws://{0}:{1}?{2}", HOSTNAME, PORT, queryString);
            return endpoint;
        }

        public GenericOutcome Disconnect()
        {
            socket.Close();
            return new GenericOutcome();
        }
    }
}
