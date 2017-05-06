// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

namespace Com.Reseul.Apis.Services.CognitiveService.Translators.UWP.Entities
{
    /// <summary>
    ///  Class representing information of text that the Translator service can provide.
    /// </summary>
    public class SpeechTextInfo
    {
        /// <summary>
        ///  Gets and sets Id that Translator Service can provide.
        /// </summary>
        public string Id;

        /// <summary>
        ///  Gets and sets Dir that Translator Service can provide.
        /// </summary>
        public string Dir;

        /// <summary>
        ///  Gets and sets Locale that Translator Service can provide.
        /// </summary>
        public string Locale;
    }
}