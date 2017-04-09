﻿// OpenTween - Client of Twitter
// Copyright (c) 2013 kim_upsilon (@kim_upsilon) <https://upsilo.net/~upsilon/>
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenTween.Api.DataModel;

namespace OpenTween.Connection
{
    public class Imgur : IMediaUploadService
    {
        private readonly static long MaxFileSize = 10L * 1024 * 1024;

        private readonly static IEnumerable<string> SupportedExtensions = new[]
        {
            ".jpg",
            ".jpeg",
            ".gif",
            ".png",
            ".tif",
            ".tiff",
            ".bmp",
            ".pdf",
            ".xcf",
        };

        private readonly Twitter twitter;
        private readonly ImgurApi imgurApi;

        private TwitterConfiguration twitterConfig;

        public Imgur(Twitter tw, TwitterConfiguration twitterConfig)
        {
            this.twitter = tw;
            this.twitterConfig = twitterConfig;

            this.imgurApi = new ImgurApi();
        }

        public int MaxMediaCount
        {
            get { return 1; }
        }

        public string SupportedFormatsStrForDialog
        {
            get
            {
                var formats = new StringBuilder();

                foreach (var extension in SupportedExtensions)
                    formats.AppendFormat("*{0};", extension);

                return "Image Files(" + formats + ")|" + formats;
            }
        }

        public bool CanUseAltText => false;

        public bool CheckFileExtension(string fileExtension)
        {
            return SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
        }

        public bool CheckFileSize(string fileExtension, long fileSize)
        {
            var maxFileSize = this.GetMaxFileSize(fileExtension);
            return maxFileSize == null || fileSize <= maxFileSize.Value;
        }

        public long? GetMaxFileSize(string fileExtension)
        {
            return MaxFileSize;
        }

        public async Task PostStatusAsync(string text, long? inReplyToStatusId, IMediaItem[] mediaItems)
        {
            if (mediaItems == null)
                throw new ArgumentNullException(nameof(mediaItems));

            if (mediaItems.Length != 1)
                throw new ArgumentOutOfRangeException(nameof(mediaItems));

            var item = mediaItems[0];

            if (item == null)
                throw new ArgumentException("Err:Media not specified.");

            if (!item.Exists)
                throw new ArgumentException("Err:Media not found.");

            XDocument xml;
            try
            {
                xml = await this.imgurApi.UploadFileAsync(item, text)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new WebApiException("Err:" + ex.Message, ex);
            }
            catch (OperationCanceledException ex)
            {
                throw new WebApiException("Err:Timeout", ex);
            }

            var imageElm = xml.Element("data");

            if (imageElm.Attribute("success").Value != "1")
                throw new WebApiException("Err:" + imageElm.Attribute("status").Value);

            var imageUrl = imageElm.Element("link").Value;

            var textWithImageUrl = text + " " + imageUrl.Trim();

            await this.twitter.PostStatus(textWithImageUrl, inReplyToStatusId)
                .ConfigureAwait(false);
        }

        public int GetReservedTextLength(int mediaCount)
            => this.twitterConfig.ShortUrlLength + 1;

        public void UpdateTwitterConfiguration(TwitterConfiguration config)
        {
            this.twitterConfig = config;
        }

        public class ImgurApi
        {
            private readonly HttpClient http;

            private static readonly Uri UploadEndpoint = new Uri("https://api.imgur.com/3/image.xml");

            public ImgurApi()
            {
                this.http = Networking.CreateHttpClient(Networking.CreateHttpClientHandler());
                this.http.Timeout = Networking.UploadImageTimeout;
            }

            public async Task<XDocument> UploadFileAsync(IMediaItem item, string title)
            {
                using (var content = new MultipartFormDataContent())
                using (var mediaStream = item.OpenRead())
                using (var mediaContent = new StreamContent(mediaStream))
                using (var titleContent = new StringContent(title))
                {
                    content.Add(mediaContent, "image", item.Name);
                    content.Add(titleContent, "title");

                    using (var request = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint))
                    {
                        request.Headers.Authorization =
                            new AuthenticationHeaderValue("Client-ID", ApplicationSettings.ImgurClientID);
                        request.Content = content;

                        using (var response = await this.http.SendAsync(request).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();

                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                return XDocument.Load(stream);
                            }
                        }
                    }
                }
            }
        }
    }
}
