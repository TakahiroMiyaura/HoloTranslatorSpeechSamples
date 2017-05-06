// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

namespace Example.Entities
{

    /// <summary>
    ///  Class representing information of speech that the Translator service can provide.
    /// </summary>
    public class SpeechLanguageInfo
    {
        /// <summary>
        ///  Gets and sets localId that Translator Service can provide.
        /// </summary>
        public string LocaleId;

        /// <summary>
        /// Gets and sets Language that Translator Service can provide.
        /// </summary>
        public string Language;

        /// <summary>
        /// Gets and sets Name that Translator Service can provide.
        /// </summary>
        public string Name;

    }
}