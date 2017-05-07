// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.IO;
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
    private List<short> samplingData = new List<short>();

    private bool _isStart;
    #endregion

    // Update is called once per frame
    void Update ()
	{
        CheckForErrorOnCall(MicStream.MicSetGain(InputGain));
        if (Input.GetKeyDown(KeyCode.W))
        {
            samplingData = new List<short>();
            CheckForErrorOnCall(MicStream.MicStartStream(KeepAllData, false));
            _isStart = true;
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            _isStart = false;
            CheckForErrorOnCall(MicStream.MicStopStream());
            WriteAudioData();

        }
	}

    private void Awake()
    {
        CheckForErrorOnCall(MicStream.MicInitializeCustomRate((int)StreamType, AudioSettings.outputSampleRate));
        CheckForErrorOnCall(MicStream.MicSetGain(InputGain));
     }
    
    private void OnAudioFilterRead(float[] buffer, int numChannels)
    {
        if (!_isStart) return;

        CheckForErrorOnCall(MicStream.MicGetFrame(buffer, buffer.Length, numChannels));
        lock (this)
        {
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
#if UNITY_EDITOR
        var file = new FileStream(@"D:\SampleByHolo.wav", FileMode.Create);


        var headerSize = 46;
        short extraSize = 0;


        short toBitsPerSample = 16;
        short toChannels = 2;
        int toSampleRate = AudioSettings.outputSampleRate;
        var blockAlign = (short)(toChannels * (toBitsPerSample / 8));
        var averageBytesPerSecond = toSampleRate * blockAlign;


        var samplingDataSize = samplingData.Count;
        var sampingDataByteSize = samplingDataSize * blockAlign * toChannels; //DataSize


        var bytes = Encoding.UTF8.GetBytes("RIFF");
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes(headerSize + sampingDataByteSize - 8);
        file.Write(bytes, 0, bytes.Length);

        bytes = Encoding.UTF8.GetBytes("WAVE");
        file.Write(bytes, 0, bytes.Length);

        bytes = Encoding.UTF8.GetBytes("fmt ");
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes(18);
        file.Write(bytes, 0, bytes.Length); // wave format length 

        bytes = BitConverter.GetBytes((short)1);
        file.Write(bytes, 0, bytes.Length); // PCM

        bytes = BitConverter.GetBytes(toChannels);
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes(toSampleRate);
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes(averageBytesPerSecond);
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes(blockAlign);
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes(toBitsPerSample);
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes(extraSize);
        file.Write(bytes, 0, bytes.Length);

        bytes = Encoding.UTF8.GetBytes("data");
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes(sampingDataByteSize);
        file.Write(bytes, 0, bytes.Length);


        for (var i = 0; i < samplingDataSize; i++)
        {
            var dat = BitConverter.GetBytes(samplingData[i]);
            file.Write(dat, 0, dat.Length);
        }
        file.Flush();
        file.Close();
#endif
    }

}
