// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Com.Reseul.Apis.Services.CognitiveService.Translators.UWP.Entities;
using Windows.Data.Json;

namespace Com.Reseul.Apis.Services.CognitiveService.Translators.UWP.Services
{
    /// <summary>
    ///     Class providing real time translation function using Translator that cognitive Service API provided.
    /// </summary>
    public class CognitiveTranslatorService
    {
        #region TokenService

        /// <summary>
        ///     Request a token to the service.
        /// </summary>
        /// <returns>Access token.</returns>
        private async Task<string> RequestToken()

        {
            if (string.IsNullOrEmpty(_subscriptionKey))
                throw new ArgumentNullException("SubscriptionKey");

            var query = new StringBuilder();
            query.Append("Subscription-Key=").Append(_subscriptionKey);

            var httpClient = new HttpClient();
            var stringContent = new StringContent("");
            using (var httpResponseMessage = await httpClient.PostAsync(TokenUrl + query, stringContent))
            {
                using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                {
                    if (stream != null)
                        using (var reader = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                        {
                            return reader.ReadToEnd();
                        }
                }
            }
            return string.Empty;
        }

        #endregion

        #region private Field

        /// <summary>
        ///     Translator WebSocket Url
        /// </summary>
        private const string SpeechTranslateUrl = @"wss://dev.microsofttranslator.com/speech/translate?";


        /// <summary>
        ///     Translator Language Info Service Url
        /// </summary>
        private const string LanguageUrl = "https://dev.microsofttranslator.com/languages?";

        /// <summary>
        ///     Cognitive Service API Token Service Url
        /// </summary>
        private const string TokenUrl = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken?";

        /// <summary>
        ///     Cognitive Service API version.
        /// </summary>
        private const string API_VERSION = "1.0";

        /// <summary>
        ///     Connect to Cognitive Service API at regular intervals.
        /// </summary>
        private Timer _connectionTimer;

        /// <summary>
        ///     Proxy server address.
        /// </summary>
        private static Uri _proxyAddress;

        /// <summary>
        ///     Proxy server username
        /// </summary>
        private static string _proxyUserName;

        /// <summary>
        ///     Proxy server user password.
        /// </summary>
        private static string _proxyPassword;

        /// <summary>
        ///     Cognitive Service API - Translator API subscription key.
        /// </summary>
        private readonly string _subscriptionKey;

        /// <summary>
        ///     number of the wave audio channels.
        /// </summary>
        private readonly short _channels;

        /// <summary>
        ///     number of the wave audio sample rate.
        /// </summary>
        private readonly int _sampleRate;
        
        /// <summary>
        ///     number of the wave audio bit per sample.
        /// </summary>
        private readonly short _bitsPerSample;

        /// <summary>
        ///     Toekn of Cognitive Service API
        /// </summary>
        private string _token;
       
        /// <summary>
        ///     a thread to periodically send data to the cognitive service API.
        /// </summary>
        private Task _dataStreamingSendThread;

        /// <summary>
        ///     Clear buffer data of Sppech.
        /// </summary>
        private bool _ClearBuffer;

        /// <summary>
        ///     When Service initialize is true,websocket initialize.
        /// </summary>
        private bool _isInitializing;

        /// <summary>
        ///     interval milliseconds of re-connection.
        /// </summary>
        private readonly double _connectionInterval = 9 * 60 * 1000;

        #endregion

        #region Event
        
        /// <summary>
        ///     Occurs when the Cpgnitive Service API receives a message.
        /// </summary>
        public EventHandler<MessageWebSocketMessageReceivedEventArgs> OnRootMessage;

        /// <summary>
        ///     Occurs when the Cpgnitive Service API receives a message of translated text.
        /// </summary>
        public EventHandler<MessageWebSocketMessageReceivedEventArgs> OnTextMessage;

        /// <summary>
        ///     Occurs when the Cpgnitive Service API receives a message of translated voice data(wave).
        /// </summary>
        public EventHandler<MessageWebSocketMessageReceivedEventArgs> OnVoiceMessage;

        /// <summary>
        ///     WebSockect object.
        /// </summary>
        private MessageWebSocket webSocket;

        /// <summary>
        ///     Buffered send data.
        /// </summary>
        private DataWriter dataWriter;

        #endregion

        #region Constructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="CognitiveTranslatorService" /> class to the value indicated by a
        ///     subscription key of Translator Speech API,and a proxy server information,and wave audio parameters.
        /// </summary>
        /// <param name="subscriptionKey">Subscription Key of Translator Speech API</param>
        /// <param name="proxyUserName">Proxy User Name.(If the proxy server requires)</param>
        /// <param name="proxyPassword">Proxy User Password.(If the proxy server requires)</param>
        /// <param name="channels">number of the wave audio channels.</param>
        /// <param name="samplerate">number of the wave audio sample rate.</param>
        /// <param name="bitsPerSample">number of the wave audio bit per sample.</param>
        public CognitiveTranslatorService(string subscriptionKey, string proxyUserName,
            string proxyPassword, short channels, int samplerate, short bitsPerSample)
        {
            _subscriptionKey = subscriptionKey;
            _proxyUserName = proxyUserName;
            _proxyPassword = proxyPassword;
            _channels = channels;
            _sampleRate = samplerate;
            _bitsPerSample = bitsPerSample;

            if (!string.IsNullOrEmpty(_proxyUserName) && !string.IsNullOrEmpty(_proxyPassword))
                WebRequest.DefaultWebProxy.Credentials = new NetworkCredential(_proxyUserName, _proxyPassword);
        }


        /// <summary>
        ///     Initializes a new instance of the <see cref="CognitiveTranslatorService" /> class to the value indicated by a
        ///     subscription key of Translator Speech API,and streaming parameters.
        /// </summary>
        /// <remarks>
        ///     Initializes a new instance of the <see cref="CognitiveTranslatorService" /> class to the value indicated by a
        ///     subscription key of Translator Speech API,and streaming parameters.
        ///     WebSocket execute no proxy.
        /// </remarks>
        /// <param name="subscriptionKey">Subscription Key of Translator Speech API</param>
        /// <param name="channels">number of the wave audio channels.</param>
        /// <param name="samplerate">number of the wave audio sample rate.</param>
        /// <param name="bitsPerSample">number of the wave audio bit per sample.</param>
        public CognitiveTranslatorService(string subscriptionKey, short channels, int samplerate, short bitsPerSample)
            : this(subscriptionKey, null, null, channels, samplerate, bitsPerSample)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="CognitiveTranslatorService" /> class to the value indicated by a
        ///     subscription key of Translator Speech API
        /// </summary>
        /// <remarks>
        ///     Initializes a new instance of the <see cref="CognitiveTranslatorService" /> class to the value indicated by a
        ///     subscription key of Translator Speech API,and streaming parameters.
        ///     WebSocket execute no proxy,and rate of wave audio is 1channel,16Khz,16bit.
        /// </remarks>
        /// <param name="subscriptionKey">Subscription Key of Translator Speech API</param>
        public CognitiveTranslatorService(string subscriptionKey)
            : this(subscriptionKey, null, null, 1, 16000, 16)
        {
        }

        #endregion

        #region WebSocket

        /// <summary>
        ///     Connect to the server before sending audio
        ///     It will get the authentication credentials and add it to the header
        /// </summary>
        /// <param name="from">sets the  launguage of source.(<see cref="SpeechLanguageInfo.Language" />)</param>
        /// <param name="to">sets the  launguage of translated results.(<see cref="SpeechLanguageInfo.Language" />)</param>
        /// <param name="voice">if you get to speech of translated result, sets the value of <see cref="SpeechTtsInfo.Id" />. </param>
        public async Task Connect(string from, string to, string voice)
        {
            if (to == null) throw new ArgumentNullException("to");
            if (from == null) throw new ArgumentNullException("from");

            if (webSocket != null)
            {
                webSocket.Dispose();
                webSocket = null;
            }
            if (_connectionTimer != null)
            {
                _connectionTimer.Dispose();
                _connectionTimer = null;
            }

            webSocket = new MessageWebSocket();

            // Get Azure authentication token
            var bearerToken = await RequestToken();

            webSocket.SetRequestHeader("Authorization", "Bearer " + bearerToken);

            var query = new StringBuilder();
            query.Append("from=").Append(from);
            query.Append("&to=").Append(to);
            if (!string.IsNullOrEmpty(voice))
                query.Append("&features=texttospeech&voice=").Append(voice);
            query.Append("&api-version=").Append(API_VERSION);


            webSocket.MessageReceived += WebSocket_MessageReceived;

            // setup the data writer
            dataWriter = new DataWriter(webSocket.OutputStream);
            dataWriter.ByteOrder = ByteOrder.LittleEndian;
            dataWriter.WriteBytes(GetWaveHeader());

            // connect to the service
            await webSocket.ConnectAsync(new Uri(SpeechTranslateUrl + query));

            //// flush the dataWriter periodically
            _connectionTimer = new Timer(async s =>
                {
                    if (dataWriter.UnstoredBufferLength > 0)
                    {
                        await dataWriter.StoreAsync();
                    }

                    // reset the timer
                    _connectionTimer.Change(TimeSpan.FromMilliseconds(250), Timeout.InfiniteTimeSpan);
                },
                null, TimeSpan.FromMilliseconds(250), Timeout.InfiniteTimeSpan);          
        }

        

        /// <summary>
        /// An event that indicates that a message was received on the MessageWebSocket object.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="args">The event data. If there is no event data, this parameter will be null.</param>
        private void WebSocket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            if (OnRootMessage != null)
                OnRootMessage(sender, args);

            if (args.MessageType == SocketMessageType.Binary)
            {
                if (OnVoiceMessage != null)
                    OnVoiceMessage(sender, args);
            }
            else
            {
                if (OnTextMessage != null)
                    OnTextMessage(sender, args);
            }
        }

        /// <summary>
        ///     add wave sampling data.
        /// </summary>
        /// <param name="buffer">The buffer to write data from. </param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing data from the current stream.</param>
        /// <param name="count">The maximum number of bytes to read. </param>
        public void AddSamplingData(byte[] buffer, int offset, int count)
        {
            dataWriter.WriteBytes(buffer);
        }

        #endregion

        #region Languages

        /// <summary>
        ///     Gets speech informations that Cognitive Service API can provide.
        /// </summary>
        /// <returns><see cref="SpeechLanguageInfo" /> object list.</returns>
        public static async Task<ReadOnlyCollection<SpeechLanguageInfo>> GetSpeechLanguageInfo()
        {
            IEnumerable<SpeechLanguageInfo> speechLanguageInfos = null;

            var query = new StringBuilder();
            query.Append("api-version=").Append(API_VERSION);
            query.Append("&scope=").Append("speech");

            var httpClient = new HttpClient();
            using (var httpResponseMessage = await httpClient.GetAsync(LanguageUrl + query))
            {
                using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                {
                    if (stream != null)
                        using (var reader = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                        {
                            var json = reader.ReadToEnd();
                            var jsonObject = JsonObject.Parse(json);
                            speechLanguageInfos = jsonObject["speech"].GetObject().Select(
                                x => new SpeechLanguageInfo()
                                {
                                    LocaleId = x.Key,
                                    Language = x.Value.GetObject()["language"].GetString(),
                                    Name = x.Value.GetObject()["name"].GetString()
                                });
                        }
                }
            }
            return new ReadOnlyCollection<SpeechLanguageInfo>(speechLanguageInfos.ToArray());
        }

        /// <summary>
        ///     Gets tts informations that Cognitive Service API can provide.
        /// </summary>
        /// <returns><see cref="SpeechTtsInfo" /> object list.</returns>
        public static async Task<ReadOnlyCollection<SpeechTtsInfo>> GetSpeechTtsInfo()
        {
            IEnumerable<SpeechTtsInfo> speechTtsInfos = null;
            var query = new StringBuilder();
            query.Append("api-version=").Append(API_VERSION);
            query.Append("&scope=").Append("tts");

            var httpClient = new HttpClient();
            using (var httpResponseMessage = await httpClient.GetAsync(LanguageUrl + query))
            {
                using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                {
                    if (stream != null)
                        using (var reader = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                        {
                            var json = reader.ReadToEnd();
                            var jsonObject = JsonObject.Parse(json);
                            speechTtsInfos = jsonObject["tts"].GetObject().Select(
                                x => new SpeechTtsInfo()
                                {
                                    Id = x.Key,
                                    Gender = x.Value.GetObject()["gender"].GetString(),
                                    Locale = x.Value.GetObject()["locale"].GetString(),
                                    LanguageName = x.Value.GetObject()["languageName"].GetString(),
                                    DisplayName = x.Value.GetObject()["displayName"].GetString(),
                                    RegionName = x.Value.GetObject()["regionName"].GetString(),
                                    Language = x.Value.GetObject()["language"].GetString()
                                });
                        }
                }
            }
            return new ReadOnlyCollection<SpeechTtsInfo>(speechTtsInfos.ToArray());
        }

        /// <summary>
        ///     Gets speech text informations that Cognitive Service API can provide.
        /// </summary>
        /// <returns><see cref="SpeechTextInfo" /> object list.</returns>
        public static async Task<ReadOnlyCollection<SpeechTextInfo>> GetSpeechTextInfo()
        {
            IEnumerable<SpeechTextInfo> speechTextInfos = null;

            var query = new StringBuilder();
            query.Append("api-version=").Append(API_VERSION);
            query.Append("&scope=").Append("text");

            var httpClient = new HttpClient();


            using (var httpResponseMessage = await httpClient.GetAsync(LanguageUrl + query))
            {
                using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                {
                    if (stream != null)
                        using (var reader = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                        {
                            var json = reader.ReadToEnd();
                            var jsonObject = JsonObject.Parse(json);
                            speechTextInfos = jsonObject["text"].GetObject().Select(
                                x => new SpeechTextInfo()
                                {
                                    Id = x.Key,
                                    Dir = x.Value.GetObject()["dir"].GetString(),
                                    Locale = x.Value.GetObject()["name"].GetString(),
                                });
                        }
                }
            }

            return new ReadOnlyCollection<SpeechTextInfo>(speechTextInfos.ToArray());
        }

        #endregion

        #region Commons

        /// <summary>
        ///     Create a RIFF Wave Header for PCM 16bit 16kHz Mono
        /// </summary>
        /// <returns></returns>
        private byte[] GetWaveHeader()
        {
            var extraSize = 0;
            var blockAlign = (short) (_channels * (_bitsPerSample / 8));
            var averageBytesPerSecond = _sampleRate * blockAlign;

            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream, Encoding.UTF8);
                writer.Write(Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(0);
                writer.Write(Encoding.UTF8.GetBytes("WAVE"));
                writer.Write(Encoding.UTF8.GetBytes("fmt "));
                writer.Write(18 + extraSize);
                writer.Write((short) 1);
                writer.Write(_channels);
                writer.Write(_sampleRate);
                writer.Write(averageBytesPerSecond);
                writer.Write(blockAlign);
                writer.Write(_bitsPerSample);
                writer.Write((short) extraSize);

                writer.Write(Encoding.UTF8.GetBytes("data"));
                writer.Write(0);

                stream.Position = 0;
                var buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                return buffer;
            }
        }
        
        #endregion
    }
}