﻿// OpenTween - Client of Twitter
// Copyright (c) 2016 kim_upsilon (@kim_upsilon) <https://upsilo.net/~upsilon/>
// All rights reserved.
//
// This file is part of OpenTween.
//
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTween.Connection
{
    public class OAuthEchoHandler : DelegatingHandler
    {
        public Uri AuthServiceProvider { get; }
        public string VerifyCredentialsAuthorization { get; }

        public OAuthEchoHandler(HttpMessageHandler innerHandler, Uri authServiceProvider, string authorizationValue)
            : base(innerHandler)
        {
            this.AuthServiceProvider = authServiceProvider;
            this.VerifyCredentialsAuthorization = authorizationValue;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("X-Auth-Service-Provider", this.AuthServiceProvider.AbsoluteUri);
            request.Headers.Add("X-Verify-Credentials-Authorization", this.VerifyCredentialsAuthorization);

            return base.SendAsync(request, cancellationToken);
        }

        public static OAuthEchoHandler CreateHandler(HttpMessageHandler innerHandler, Uri authServiceProvider,
            string consumerKey, string consumerSecret, string accessToken, string accessSecret, Uri realm = null)
        {
            var credential = OAuthUtility.CreateAuthorization("GET", authServiceProvider, null,
                consumerKey, consumerSecret, accessToken, accessSecret, realm?.AbsoluteUri);

            return new OAuthEchoHandler(innerHandler, authServiceProvider, credential);
        }
    }
}
