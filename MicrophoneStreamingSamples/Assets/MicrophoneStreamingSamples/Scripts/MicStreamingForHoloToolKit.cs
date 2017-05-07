// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
#else
using Windows.UI.Core;
using Windows.Storage;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
#endif
using System.Text;
using HoloToolkit.Unity.InputModule;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicStreamingForHoloToolKit : MonoBehaviour {

    #region Public Parameter
    /// <summary>
    /// Which type of microphone/quality to access
    /// </summary>
    public MicStream.StreamCategory StreamType = MicStream.StreamCategory.HIGH_QUALITY_VOICE;

    /// <summary>
    /// if keepAllData==false, you'll always get the newest data no matter how long the program hangs for any reason, but will lose some data if the program does hang 
    /// can only be set on initialization
    /// </summary>
    public bool KeepAllData = false;

    /// <summary>
    /// can boost volume here as desired. 1 is default but probably too quiet. can change during operation. 
    /// </summary>
    public float InputGain = 1;

    #endregion

    #region private field
    private readonly List<short> samplingData = new List<short>();

    private bool _isStart = false;

#if !UNITY_EDITOR
    private Task task;
#endif

    #endregion

    // Update is called once per frame
    void Update ()
	{
        if (Input.GetKeyDown(KeyCode.W))
        {
            samplingData.Clear();
            CheckForErrorOnCall(MicStream.MicStartStream(KeepAllData, false));
            CheckForErrorOnCall(MicStream.MicSetGain(InputGain));

            _isStart = true;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            _isStart = false;
            CheckForErrorOnCall(MicStream.MicStopStream());
            WriteAudioData();

        }
	}

    private void OnDestroy()
    {
        CheckForErrorOnCall(MicStream.MicDestroy());
    }


    private void Awake()
    {
        CheckForErrorOnCall(MicStream.MicInitializeCustomRate((int)StreamType, AudioSettings.outputSampleRate));
     }
    
    private void OnAudioFilterRead(float[] buffer, int numChannels)
    {
        if (!_isStart) return;
        lock (this)
        {
            CheckForErrorOnCall(MicStream.MicGetFrame(buffer, buffer.Length, numChannels));

            foreach (var f in buffer)
            {
                samplingData.Add(FloatToInt16(f));
            }
        }
    }

    /// <summary>
    ///     The bytes that we get from audiograph is in IEEE float, 
    /// </summary>
    /// <param name="value">sampling Data</param>
    /// <returns>waveform data(16bit)</returns>
    private static short FloatToInt16(float value)
    {
        var f = value * short.MaxValue;
        if (f > short.MaxValue) f = short.MaxValue;
        if (f < short.MinValue) f = short.MinValue;
        return (short)f;
    }
    
    private void CheckForErrorOnCall(int returnCode)
    {
        MicStream.CheckForErrorOnCall(returnCode);
    }

    /// <summary>
    /// samplingData output sample code.
    /// </summary>
    private void WriteAudioData()
    {
        var fileName = "StreamingDataHolo.wav";
        var headerSize = 46;
        short extraSize = 0;

        short toBitsPerSample = 16;
        short toChannels = 2;
        int toSampleRate = AudioSettings.outputSampleRate;
        var blockAlign = (short)(toChannels * (toBitsPerSample / 8));
        var averageBytesPerSecond = toSampleRate * blockAlign;


        var samplingDataSize = samplingData.Count;
        var sampingDataByteSize = samplingDataSize * blockAlign ; //DataSize
        
#if UNITY_EDITOR
        using (var file = new FileStream(@"D:\" + fileName, FileMode.Create))
        {
#else
        task = Task.Run(async () =>
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            using (var outputStrm = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
#endif

            var bytes = Encoding.UTF8.GetBytes("RIFF");
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(headerSize + sampingDataByteSize - 8);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = Encoding.UTF8.GetBytes("WAVE");
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = Encoding.UTF8.GetBytes("fmt ");
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(18);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes((short)1);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(toChannels);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(toSampleRate);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(averageBytesPerSecond);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(blockAlign);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(toBitsPerSample);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(extraSize);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = Encoding.UTF8.GetBytes("data");
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            
            bytes = BitConverter.GetBytes(sampingDataByteSize);
#if UNITY_EDITOR
            file.Write(bytes, 0, bytes.Length);
#else
            await outputStrm.WriteAsync(bytes.AsBuffer());
#endif            


            for (var i = 0; i < samplingDataSize; i++)
            {
                var dat = BitConverter.GetBytes(samplingData[i]);
#if UNITY_EDITOR
                file.Write(dat, 0, dat.Length);
#else
                await outputStrm.WriteAsync(dat.AsBuffer());
#endif
            }
        }
#if !UNITY_EDITOR
        });
        task.Wait();
#endif
    }

}
