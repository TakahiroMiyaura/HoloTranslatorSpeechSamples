// Copyright(c) 2017 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
#endif
using System.Text;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicStreaming : MonoBehaviour
{

    #region private field

    private new AudioSource audio;

    private string deviceName;

#if UNITY_EDITOR
    private readonly List<short> samplingData = new List<short>();
#endif
    
    private bool _isStart;
    #endregion

    // Use this for initialization
    private void Start()
    {
        //get device name.
        deviceName = Microphone.devices[0];
        
    }
    
    // Update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            samplingData.Clear();
            //set microphone.
            audio = GetComponent<AudioSource>();
            audio.clip = Microphone.Start(deviceName, false, 999, AudioSettings.outputSampleRate);
            audio.loop = true;
            while (!(Microphone.GetPosition(deviceName) > 0)) { }

            //recording start.
            audio.Play();
            _isStart = true;
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            _isStart = false;
            audio.Stop();
            Microphone.End(deviceName);
            WriteAudioData();

        }
    }
    
    private void OnAudioFilterRead(float[] buffer, int numChannels)
    {
        if (!_isStart) return;
        
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
        return (short) f;
    }

    /// <summary>
    /// samplingData output sample code.
    /// </summary>
    private void WriteAudioData()
    {
#if UNITY_EDITOR
        var file = new FileStream(@"D:\Sample.wav", FileMode.Create);


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
        file.Write(bytes, 0, bytes.Length);

        bytes = BitConverter.GetBytes((short)1);
        file.Write(bytes, 0, bytes.Length);

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