// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System;
using System.IO;
using System.Text;
using System.Threading;
using Com.Reseul.Apis.Services.CognitiveService.Translators.Services;
using NAudio.Wave;
using WebSocketSharp;

namespace Example
{
    public class Program
    {
        private const string SUNSCRIPTION_KEY = "[subscription Key]";
        private static Thread _thread;
        private static Thread _thread2;

        /// <summary>
        ///     The bytes that we get from audiograph is in IEEE float, we need to covert that to 16 bit
        ///     before sending it to the speech translate service
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static short FloatToInt16(float value)
        {
            var f = value * short.MaxValue;
            if (f > short.MaxValue) f = short.MaxValue;
            if (f < short.MinValue) f = short.MinValue;
            return (short) f;
        }

        public static void Main(string[] args)
        {
            var speechLanguageInfos = CognitiveTranslatorService.GetSpeechLanguageInfo();
            var readOnlyCollection = CognitiveTranslatorService.GetSpeechTextInfo();
            var speechTtsInfos = CognitiveTranslatorService.GetSpeechTtsInfo();

            var service = new CognitiveTranslatorService(SUNSCRIPTION_KEY);
            //TODO: Modify Translate Language settings. 
            service.InitializeTranslatorService("en","ja", "ja-JP-Watanabe");
            service.OnOpen += OnOpen;
            service.OnError += OnError;
            service.OnRootMessage+= OnRootMessage;
            service.OnVoiceMessage += OnVoiceMessage;
            service.OnTextMessage += OnTextMessage;
            
#if DEBUG
            service.LogLevel = LogLevel.Trace;
#endif

            service.StartStreaming();

            Thread.Sleep(1000);

                _thread = new Thread(() =>
                {
                    //TODO: change file name.sampling rate-16Khz 16bit 1channel.
                    using (var reader = new WaveFileReader("something.wav"))
                    {
                        Thread.Sleep(1000);
                        var samplesL = new float[reader.Length / reader.BlockAlign];
                        for (var i = 0; i < samplesL.Length; i++)
                        {
                            var sample = reader.ReadNextSampleFrame();

                            var bytes = BitConverter.GetBytes(FloatToInt16(sample[0]));
                            service.AddSamples(bytes, 0, bytes.Length);
                        }
                        for (int i = 0; i < 32000; i++)
                        {

                            service.AddSamples(new byte[] {0, 0}, 0, 2);
                        }
                    }
                });
                _thread.Start();

            _thread2 = new Thread(new ThreadStart(() =>
            {
                Console.ReadLine();service.StopStreaming();Thread.Sleep(1000);Environment.Exit(0);
            }));
            _thread2.Start();
            while (true)
            {

            }
        }

        private static void OnTextMessage(object sender, MessageEventArgs messageEventArgs)
        {

            var result = "";
            using (var stream = new MemoryStream())
            {
                var rawData = messageEventArgs.RawData;
                stream.Write(rawData, 0, rawData.Length);
                stream.Position = 0;
                using (var sr = new StreamReader(stream, Encoding.GetEncoding("UTF-8")))
                {
                    result = sr.ReadToEnd();
                }
            }
            Console.WriteLine(result);
        }

        private static void OnVoiceMessage(object sender, MessageEventArgs messageEventArgs)
        {

            using (var stream = new MemoryStream())
            {
                var rawData = messageEventArgs.RawData;
                stream.Write(rawData, 0, rawData.Length);
                stream.Position = 0;
                using (var player = new WaveOutEvent())
                using (var rdr = new WaveFileReader(stream))
                using (var wavStream = WaveFormatConversionStream.CreatePcmStream(rdr))
                using (var baStream = new BlockAlignReductionStream(wavStream))
                {
                    player.Init(baStream);

                    player.Play();
                    while (player.PlaybackState == PlaybackState.Playing)
                    {
                    }
                }
            }
        }

        private static void OnRootMessage(object sender, MessageEventArgs messageEventArgs)
        {
            Console.WriteLine("OnRootMessage");
        }

        private static void OnError(object sender, EventArgs errorEventArgs)
        {
            Console.WriteLine("OnError");
        }

        private static void OnOpen(object sender, EventArgs eventArgs)
        {
            Console.WriteLine("OnOpen");
        }
        }
}