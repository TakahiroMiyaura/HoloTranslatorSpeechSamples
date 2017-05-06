// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Com.Reseul.Apis.Services.CognitiveService.Translators.Entities;
using Com.Reseul.Apis.Services.CognitiveTranslators.Entities;
using Example;
using Example.Entities;
using LitJson;
using NAudio.Wave;
using WebSocketSharp;

namespace Com.Reseul.Apis.Services.CognitiveService.Translators.Services
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
        /// <returns></returns>
        private string RequestToken()
        {
            if (string.IsNullOrEmpty(_subscriptionKey))
                throw new ArgumentNullException("SubscriptionKey");

            var query = new StringBuilder();
            query.Append("Subscription-Key=").Append(_subscriptionKey);


            var tokenRequest = CreateWebRequest(TokenUrl + query);
            tokenRequest.Method = "POST";
            tokenRequest.ContentType = "application/x-www-form-urlencoded";
            tokenRequest.ContentLength = 0;

            using (var tokenResponse = (HttpWebResponse) tokenRequest.GetResponse())
            {
                using (var stream = tokenResponse.GetResponseStream())
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
        ///     Lock Object.
        /// </summary>
        private static readonly object _lockObject = new object();

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
        ///     Buffered wave data.
        /// </summary>
        private readonly BufferedWaveProvider _bufferedWaveProvider;

        /// <summary>
        ///     number of the wave audio bit per sample.
        /// </summary>
        private readonly short _bitsPerSample;

        /// <summary>
        ///     Toekn of Cognitive Service API
        /// </summary>
        private string _token;

        /// <summary>
        ///     WebSockect object.
        /// </summary>
        private WebSocket _webSocket;
        
        /// <summary>
        ///     a thread to periodically send data to the cognitive service API.
        /// </summary>
        private Thread _dataStreamingSendThread;

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

        #region Propeties

        /// <summary>
        ///     Gets Web socket object.
        /// </summary>
        private WebSocket Socket

        {
            get
            {
                while (_isInitializing)
                    Thread.Sleep(50);
                return _webSocket;
            }
        }

        /// <summary>
        ///     output Log Level.
        /// </summary>
        public LogLevel LogLevel
        {
            set
            {
                if (Socket != null)
                {
                    _logLevel = value;
                    Socket.Log.Level = value;
                }
            }
        }

        #endregion

        #region Event

        /// <summary>
        ///     Occurs when the Cpgnitive Service API connection has been established.
        /// </summary>
        public EventHandler OnOpen;

        ///// <summary>
        ///// Occurs when the <see cref="WebSocket"/> gets an error.
        ///// </summary>
        public EventHandler<EventArgs> OnError;

        /// <summary>
        ///     Occurs when the Cpgnitive Service API receives a message.
        /// </summary>
        public EventHandler<MessageEventArgs> OnRootMessage;

        /// <summary>
        ///     Occurs when the Cpgnitive Service API receives a message of translated text.
        /// </summary>
        public EventHandler<MessageEventArgs> OnTextMessage;

        /// <summary>
        ///     Occurs when the Cpgnitive Service API receives a message of translated voice data(wave).
        /// </summary>
        public EventHandler<MessageEventArgs> OnVoiceMessage;

        #endregion

        #region Constructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="CognitiveTranslatorService" /> class to the value indicated by a
        ///     subscription key of Translator Speech API,and a proxy server information,and wave audio parameters.
        /// </summary>
        /// <param name="subscriptionKey">Subscription Key of Translator Speech API</param>
        /// <param name="proxyAddress">Proxy Server Address(ex:http//proxy.com).</param>
        /// <param name="proxyUserName">Proxy User Name.(If the proxy server requires)</param>
        /// <param name="proxyPassword">Proxy User Password.(If the proxy server requires)</param>
        /// <param name="channels">number of the wave audio channels.</param>
        /// <param name="samplerate">number of the wave audio sample rate.</param>
        /// <param name="bitsPerSample">number of the wave audio bit per sample.</param>
        public CognitiveTranslatorService(string subscriptionKey, Uri proxyAddress, string proxyUserName,
            string proxyPassword, short channels, int samplerate, short bitsPerSample)
        {
            _subscriptionKey = subscriptionKey;
            _proxyAddress = proxyAddress;
            _proxyUserName = proxyUserName;
            _proxyPassword = proxyPassword;
            _channels = channels;
            _sampleRate = samplerate;
            _bitsPerSample = bitsPerSample;


            _bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(_sampleRate, _bitsPerSample, _channels));

            _bufferedWaveProvider.BufferLength = int.MaxValue / 2;
            _bufferedWaveProvider.DiscardOnBufferOverflow = true;
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
            : this(subscriptionKey, null, null, null, channels, samplerate, bitsPerSample)
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
            : this(subscriptionKey, null, null, null, 1, 16000, 16)
        {
        }

        #endregion

        #region WebSocket

        /// <summary>
        ///     Initialize Cognitive Service API.
        /// </summary>
        /// <param name="from">sets the  launguage of source.(<see cref="SpeechLanguageInfo.Language" />)</param>
        /// <param name="to">sets the  launguage of translated results.(<see cref="SpeechLanguageInfo.Language" />)</param>
        /// <param name="voice">if you get to speech of translated result, sets the value of <see cref="SpeechTtsInfo.Id" />. </param>
        public void InitializeTranslatorService(string from, string to, string voice)
        {
            if (to == null) throw new ArgumentNullException("to");
            if (from == null) throw new ArgumentNullException("from");
            _token = RequestToken();
            var query = new StringBuilder();
            query.Append("from=").Append(from);
            query.Append("&to=").Append(to);
            if (!string.IsNullOrEmpty(voice))
                query.Append("&features=texttospeech&voice=").Append(voice);
            query.Append("&api-version=").Append(API_VERSION);
            query.Append("&access_token=").Append(_token);
            if (Socket != null)
                Socket.Close();
            if (_connectionTimer != null)
                _connectionTimer.Dispose();
            _connectionTimer = new Timer(InitializeService, query, 0, Timeout.Infinite);
            _isInitializing = true;
            while (_isInitializing)
                Thread.Sleep(100);
        }

        /// <summary>
        ///     Initialize Cognitive Service API.(Text only)
        /// </summary>
        /// <param name="from">sets the  launguage of source.(<see cref="SpeechLanguageInfo.Language" />)</param>
        /// <param name="to">sets the  launguage of translated results.(<see cref="SpeechLanguageInfo.Language" />)</param>
        public void InitializeTranslatorService(string to, string from)
        {
            InitializeTranslatorService(to, from, null);
        }

        /// <summary>
        ///     Websocke Initialize.
        /// </summary>
        /// <param name="query">sets query of Cognitive Service API.</param>
        private void InitializeService(object query)
        {
            _isInitializing = true;
            var webSocketIsAlive = false;
            if (_webSocket != null)
                webSocketIsAlive = _webSocket.IsAlive;
            if (!webSocketIsAlive)
            {
                        _webSocket = new WebSocket(SpeechTranslateUrl + query);
                _webSocket.Log.Level = LogLevel.Trace;
                if (_logLevel.HasValue)
                    _webSocket.Log.Level = _logLevel.Value;
                if (_proxyAddress != null)
                    _webSocket.SetProxy(_proxyAddress.AbsoluteUri, _proxyUserName, _proxyPassword);
                _webSocket.OnOpen += (s, e) =>
                {
                    if (OnOpen != null)
                        OnOpen(s, e);
                };
                _webSocket.OnMessage += (s, e) =>
                {
                    if (OnRootMessage != null)
                        OnRootMessage(s, e);

                    if (e.RawData[0] == 0x52
                        && e.RawData[1] == 0x49
                        && e.RawData[2] == 0x46
                        && e.RawData[3] == 0x46)
                    {
                        if (OnVoiceMessage != null)
                            OnVoiceMessage(s, e);
                    }
                    else
                    {
                        if (OnTextMessage != null)
                            OnTextMessage(s, e);
                    }
                };
                _webSocket.OnError += (s, e) =>
                {
                    if (OnError != null)
                        OnError(s, e);
                };
                _webSocket.Connect();
                Thread.Sleep(100);
                var socketIsAlive = _webSocket.IsAlive;
                _ClearBuffer = true;
                _isInitializing = false;
            }
            ResetConnectionInterval();
        }

        /// <summary>
        ///     Reset the next connection timing.
        /// </summary>
        private void ResetConnectionInterval()
        {
            _connectionTimer.Change(TimeSpan.FromMilliseconds(_connectionInterval), TimeSpan.FromMilliseconds(-1));
        }

        /// <summary>
        ///     Stop streaming send.
        /// </summary>
        public void StopStreaming()
        {
            if (_dataStreamingSendThread != null)
            {
                Socket.Log.Trace("Streaming is abort.");
                _dataStreamingSendThread.Abort();
                _dataStreamingSendThread = null;
            }
        }

        
        private LogLevel? _logLevel;

        /// <summary>
        ///     Send data.
        /// </summary>
        private void StreamingSendData()
        {
            while (true)
            {
                lock (_lockObject)
                {
                    var bufferSize = _sampleRate * _channels * (_bitsPerSample / 8);
                    if (_bufferedWaveProvider.BufferedBytes > bufferSize)
                    {
                        var sendBytes = new byte[bufferSize];
                        var before = _bufferedWaveProvider.BufferedBytes;
                        _bufferedWaveProvider.Read(sendBytes, 0, sendBytes.Length);

                        Socket.Log.Trace("before:" + before + " after:" + _bufferedWaveProvider.BufferedBytes);

                        if (_ClearBuffer)
                        {
                            Socket.Log.Trace("Send Wave Chunk.");
                            Socket.Send(GetWaveHeader());
                            _ClearBuffer = false;
                        }

                        Socket.Log.Trace("Send data.");
                        Socket.Send(sendBytes);
                        ResetConnectionInterval();
                    }
                }
            }
        }

        /// <summary>
        ///     Start streaming send.
        /// </summary>
        public void StartStreaming()
        {
            StopStreaming();
            _dataStreamingSendThread = new Thread(StreamingSendData);
            _dataStreamingSendThread.Start();
        }

        /// <summary>
        ///     add wave sampling data.
        /// </summary>
        /// <param name="buffer">The buffer to write data from. </param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing data from the current stream.</param>
        /// <param name="count">The maximum number of bytes to read. </param>
        public void AddSamples(byte[] buffer, int offset, int count)
        {
            _bufferedWaveProvider.AddSamples(buffer, offset, count);
        }

        #endregion

        #region Languages

        /// <summary>
        ///     Gets speech informations that Cognitive Service API can provide.
        /// </summary>
        /// <returns><see cref="SpeechLanguageInfo" /> object list.</returns>
        public static ReadOnlyCollection<SpeechLanguageInfo> GetSpeechLanguageInfo()
        {
            var speechLanguageInfos = new List<SpeechLanguageInfo>();

            var query = new StringBuilder();
            query.Append("api-version=").Append(API_VERSION);
            query.Append("&scope=").Append("speech");

            var request = CreateWebRequest(LanguageUrl + query);
            request.Method = "Get";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = 0;

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    if (stream != null)
                        using (var reader = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                        {
                            var json = reader.ReadToEnd();

                            var data = JsonMapper.ToObject(json);
                            foreach (KeyValuePair<string, JsonData> property in data["speech"])
                            {
                                var languageInfo = new SpeechLanguageInfo
                                {
                                    LocaleId = property.Key,
                                    Language = property.Value["language"].ToString(),
                                    Name = property.Value["name"].ToString()
                                };
                                speechLanguageInfos.Add(languageInfo);
                            }
                        }
                }
            }
            return new ReadOnlyCollection<SpeechLanguageInfo>(speechLanguageInfos);
        }

        /// <summary>
        ///     Gets tts informations that Cognitive Service API can provide.
        /// </summary>
        /// <returns><see cref="SpeechTtsInfo" /> object list.</returns>
        public static ReadOnlyCollection<SpeechTtsInfo> GetSpeechTtsInfo()
        {
            var speechTtsInfos = new List<SpeechTtsInfo>();
            var query = new StringBuilder();
            query.Append("api-version=").Append(API_VERSION);
            query.Append("&scope=").Append("tts");

            var request = CreateWebRequest(LanguageUrl + query);
            request.Method = "Get";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = 0;
            using (var response = (HttpWebResponse) request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    if (stream != null)
                        using (var reader = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                        {
                            var json = reader.ReadToEnd();
                            var data = JsonMapper.ToObject(json);
                            foreach (KeyValuePair<string, JsonData> property in data["tts"])
                            {
                                var languageInfo = new SpeechTtsInfo
                                {
                                    Id = property.Key,
                                    Gender = property.Value["gender"].ToString(),
                                    Locale = property.Value["locale"].ToString(),
                                    LanguageName = property.Value["languageName"].ToString(),
                                    DisplayName = property.Value["displayName"].ToString(),
                                    RegionName = property.Value["regionName"].ToString(),
                                    Language = property.Value["language"].ToString()
                                };
                                speechTtsInfos.Add(languageInfo);
                            }
                        }
                }
            }
            return new ReadOnlyCollection<SpeechTtsInfo>(speechTtsInfos);
        }

        /// <summary>
        ///     Gets speech text informations that Cognitive Service API can provide.
        /// </summary>
        /// <returns><see cref="SpeechTextInfo" /> object list.</returns>
        public static ReadOnlyCollection<SpeechTextInfo> GetSpeechTextInfo()
        {
            var speechTextInfos = new List<SpeechTextInfo>();

            var query = new StringBuilder();
            query.Append("api-version=").Append(API_VERSION);
            query.Append("&scope=").Append("text");

            var request = CreateWebRequest(LanguageUrl + query);
            request.Method = "Get";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = 0;

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                using (var stream = response.GetResponseStream())
                {
                    if (stream != null)
                        using (var reader = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                        {
                            var json = reader.ReadToEnd();
                            var data = JsonMapper.ToObject(json);
                            foreach (KeyValuePair<string, JsonData> property in data["text"])
                            {
                                var languageInfo = new SpeechTextInfo
                                {
                                    Id = property.Key,
                                    Dir = property.Value["dir"].ToString(),
                                    Locale = property.Value["name"].ToString()
                                };
                                speechTextInfos.Add(languageInfo);
                            }
                        }
                }
            }
            return new ReadOnlyCollection<SpeechTextInfo>(speechTextInfos);
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
                writer.Write(18 + extraSize); // wave format length 
                writer.Write((short) 1); // PCM
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

        /// <summary>
        ///     Create http request.
        /// </summary>
        /// <param name="url">Url</param>
        /// <returns><see cref="HttpWebRequest" /> object</returns>
        public static HttpWebRequest CreateWebRequest(string url)
        {
            var request = (HttpWebRequest) WebRequest.Create(url);
            if (_proxyAddress != null)
            {
                var credential = new NetworkCredential();
                credential.UserName = _proxyUserName;
                credential.Password = _proxyPassword;
                request.Proxy = new WebProxy(_proxyAddress, true, new string[] {}, credential);
            }

            return request;
        }

        #endregion
    }
}