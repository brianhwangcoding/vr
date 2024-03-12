/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using System.Collections.Generic;
using Meta.Voice;
using Meta.WitAi.Composer.Interfaces;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;

namespace Meta.WitAi.Composer.Integrations
{
    public class WitComposerRequestHandler : System.Object, IComposerRequestHandler
    {
        // Configuration data
        private IWitRequestConfiguration _configuration;

        /// <summary>
        /// Overrides request uri callback to handle composer based request
        /// </summary>
        /// <param name="request">Request used to perform composer functionality if desired</param>
        /// <param name="sessionData">Session data including composer, voice service, and more</param>
        public void OnComposerRequestSetup(ComposerSessionData sessionData, VoiceServiceRequest request)
        {
            // Invalid
            if (request == null || sessionData.composer == null || sessionData.composer.VoiceService == null)
            {
                return;
            }

            // Get configuration
            _configuration = sessionData.composer.VoiceService.WitConfiguration;

            // Add parameters
            request.Options.QueryParams[WitComposerConstants.ENDPOINT_COMPOSER_PARAM_SESSION] = sessionData.sessionID;
            request.Options.QueryParams[WitComposerConstants.ENDPOINT_COMPOSER_MESSAGE_TAG] = sessionData.versionTag;

            if (sessionData.composer.VoiceService.UsePlatformIntegrations)
            {
                request.Options.QueryParams[WitComposerConstants.PI_COMPOSER_ENABLE] =
                    WitComposerConstants.PI_COMPOSER_ENABLE_ON;
            }

            bool messageIsEmpty = false;
            // Event adjustments
            if (request.InputType == NLPRequestInputType.Text)
            {
                // Default to raw message
                var message = request.Options.Text;

                // Determine if event state
                bool isEvent = sessionData.composer.IsEventJson(message);

                // If not event or empty, serialize into message json
                messageIsEmpty = string.IsNullOrEmpty(message);
                if (!isEvent || messageIsEmpty)
                {
                    Dictionary<string, string> contents = new Dictionary<string, string>();
                    contents[WitComposerConstants.ENDPOINT_COMPOSER_MESSAGE_PARAM_MESSAGE] = message;
                    contents[WitComposerConstants.ENDPOINT_COMPOSER_MESSAGE_PARAM_TYPE] =
                        WitComposerMessageType.Message.ToString().ToLower();
                    message = JsonConvert.SerializeObject(contents);
                }

                // Update message text
                request.Options.Text = message;

                // Remove message key if possible
                if (request.Options.QueryParams.ContainsKey(WitConstants.ENDPOINT_MESSAGE_PARAM))
                {
                    request.Options.QueryParams.Remove(WitConstants.ENDPOINT_MESSAGE_PARAM);
                }

                // Set content type and post data on WitRequest
                if (request is WitRequest wr)
                {
                    wr.postContentType = "application/json";
                    wr.postData = Encoding.UTF8.GetBytes(message);
                }
                // Set content type and post
                else if (request is WitUnityRequest wur)
                {
                    wur.Endpoint = _configuration == null ? WitComposerConstants.ENDPOINT_COMPOSER_MESSAGE : _configuration.GetEndpointInfo().Event;
                    wur.ShouldPost = true;
                }
            }

            //Finally, update the context_map event flag and add to parameters.
            sessionData.contextMap?.SetData(sessionData.composer.contextMapEventKey,messageIsEmpty.ToString().ToLower());
            request.Options.QueryParams[WitComposerConstants.ENDPOINT_COMPOSER_PARAM_CONTEXT_MAP] = sessionData.contextMap?.GetJson();

            // Override Uri
            if (request is WitRequest)
            {
                WitVRequest.OnProvideCustomUri += OnCustomUri;
            }
        }
        // Override for WitRequest
        private UriBuilder OnCustomUri(UriBuilder builder)
        {
            WitVRequest.OnProvideCustomUri -= OnCustomUri;
            if (string.Equals(builder.Path, WitConstants.ENDPOINT_SPEECH))
            {
                builder.Path = _configuration == null ? WitComposerConstants.ENDPOINT_COMPOSER_SPEECH : _configuration.GetEndpointInfo().Converse;
            }
            else if (string.Equals(builder.Path, WitConstants.ENDPOINT_MESSAGE))
            {
                builder.Path = _configuration == null ? WitComposerConstants.ENDPOINT_COMPOSER_MESSAGE : _configuration.GetEndpointInfo().Event;
            }
            return builder;
        }
    }
}
