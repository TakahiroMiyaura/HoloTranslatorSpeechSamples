// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Com.Reseul.Apis.Services.CognitiveService.Translators.UWP.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Win8.Wave.WaveOutputs;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace ExamplesApp
{
    /// <summary>
    ///     それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string SUBSCRIPTION_KEY = "[Subscription Key]";
        private Task _task;

        public MainPage()
        {
            InitializeComponent();
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            var service = new CognitiveTranslatorService(SUBSCRIPTION_KEY,1,16000,16);
            var speechTextInfo = await CognitiveTranslatorService.GetSpeechTextInfo();
            service.OnRootMessage += OnRootMessage;
            service.OnVoiceMessage += OnVoiceMessage;
            service.OnTextMessage += OnTextMessage;
            //TODO: Modify Translate Language settings. 
            await service.Connect("en", "ja", "ja-JP-Watanabe");


            Task.Delay(1000);
            _task = new Task(async () =>
            {
                var currentInstalledLocation = ApplicationData.Current.LocalFolder.Path;
                //TODO: change file name.sampling rate-16Khz 16bit 1channel.
                var file = currentInstalledLocation + @"\something.wav";

                using (var waveStream = new FileStream(file, FileMode.Open))
                {
                    var reader = new RawSourceWaveStream(waveStream, new WaveFormat(16000, 16, 1));

                    var buffer = new byte[reader.Length];
                    var bytesRead = reader.Read(buffer, 0, buffer.Length);

                    var samplesL = new float[bytesRead / reader.BlockAlign];

                    switch (reader.WaveFormat.BitsPerSample)
                    {
                        case 8:
                            for (var i = 0; i < samplesL.Length; i++)
                                samplesL[i] = (buffer[i * reader.BlockAlign] - 128) / 128f;
                            break;

                        case 16:
                            for (var i = 0; i < samplesL.Length; i++)
                                samplesL[i] = BitConverter.ToInt16(buffer, i * reader.BlockAlign) / 32768f;
                            break;

                        case 32: 
                            for (var i = 0; i < samplesL.Length; i++)
                                samplesL[i] = BitConverter.ToSingle(buffer, i * reader.BlockAlign);
                            break;
                    }
                    var w = new byte[16000];
                    for (var i = 0; i < w.Length; i++)
                        w[i] = 0;
                    var data = 1000;
                    service.AddSamplingData(buffer, 0, 32000);
                    await Task.Delay(data);
                    service.AddSamplingData(buffer, 32000, 32000);
                    await Task.Delay(data);
                    service.AddSamplingData(buffer, 64000, 32000);
                    await Task.Delay(data);
                    service.AddSamplingData(buffer, 96000, buffer.Length - 96000);
                    await Task.Delay(data);
                    service.AddSamplingData(w, 0, w.Length);
                }
            });
            _task.Start();
        }

        private void OnError(object sender, EventArgs eventArgs)
        {
            ResultData.Text = ((Exception) sender).ToString();
        }
        

        private async void OnTextMessage(object sender, MessageWebSocketMessageReceivedEventArgs messageEventArgs)
        {
            var result = "";
            using (var reader = messageEventArgs.GetDataStream())
            using (var sr = new StreamReader(reader.AsStreamForRead(), Encoding.GetEncoding("UTF-8")))
            {
                result = sr.ReadToEnd();
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { ResultData.Text = result; });
            }
        }

        private void OnVoiceMessage(object sender, MessageWebSocketMessageReceivedEventArgs messageEventArgs)
        {
            using (var reader = messageEventArgs.GetDataStream())
            using (var stream = reader.AsStreamForRead())
            using (var mStream = new MemoryStream())
            {
                var bufferSize = 32000;
                var bytes = new List<byte>();
                var buf = new byte[bufferSize];
                var length = stream.Read(buf, 0, buf.Length);
                while (length - bufferSize == 0)
                {
                    bytes.AddRange(buf);
                    length = stream.Read(buf, 0, buf.Length);
                }
                if (length > 0)
                    bytes.AddRange(buf.Take(length).ToArray());

                var fullData = bytes.ToArray();
                mStream.Write(fullData, 0, fullData.Length);
                mStream.Position = 0;
                var bitsPerSampleBytes = fullData.Skip(34).Take(2).ToArray();
                var channelBytes = fullData.Skip(22).Take(2).ToArray();
                var samplingBytes = fullData.Skip(24).Take(4).ToArray();
                var bitsPerSample = BitConverter.ToInt16(bitsPerSampleBytes, 0);
                var channel = BitConverter.ToInt16(channelBytes, 0);
                var samplingRate = BitConverter.ToInt32(samplingBytes, 0);

                using (var player = new WasapiOutRT(AudioClientShareMode.Shared, 250))
                {
                    player.Init(() =>
                    {
                        var waveChannel32 =
                            new WaveChannel32(new RawSourceWaveStream(mStream,
                                new WaveFormat(samplingRate, bitsPerSample, channel)));
                        var mixer = new MixingSampleProvider(new[] {waveChannel32.ToSampleProvider()});

                        return mixer.ToWaveProvider16();
                    });

                    player.Play();
                    while (player.PlaybackState == PlaybackState.Playing)
                    {
                    }
                }
            }
        }

        private void OnRootMessage(object sender, MessageWebSocketMessageReceivedEventArgs e)
        {
        }
    }
}