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

namespace Aws.GameLift.Server.Model
{
    public class GetInstanceCertificateResult
    {
        public string CertificatePath { get; set; }
        public string PrivateKeyPath { get; set; }
        public string CertificateChainPath { get; set; }
        public string HostName { get; set; }
        public string RootCertificatePath { get; set; }

        public GetInstanceCertificateResult() {}

        public static GetInstanceCertificateResult ParseFromBufferedGetInstanceCertificateResponse(Com.Amazon.Whitewater.Auxproxy.Pbuffer.GetInstanceCertificateResponse response)
        {
            var translation = new GetInstanceCertificateResult();

            translation.CertificatePath = response.CertificatePath;
            translation.PrivateKeyPath = response.PrivateKeyPath;
            translation.CertificateChainPath = response.CertificateChainPath;
            translation.HostName = response.HostName;
            translation.RootCertificatePath = response.RootCertificatePath;

            return translation;
        }
    }
}
