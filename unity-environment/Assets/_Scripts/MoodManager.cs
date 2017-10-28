﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MoodManager : Singleton<MoodManager> 
{
    AudioSource audioSource;
    [Header("Clamping dB values")]
    public float min_dB = -25.0f;
    public float max_dB = 25.0f;

    [Header("The all seeing, all knowing mood value that we must edit")]
    public float MoodValue = 0.5f;

    [Header("File paths for output data files")]
    public string lowValueFilePath = "Assets/_Data/lowVals.txt";
    public string highValueFilePath = "Assets/_Data/highVals.txt";

    private int qSamples = 1024;
    private float refRmsVal = 0.1f; // represents the rms value that corresponds to 0 dB
    private float minSpectrumVal = 0.02f;

    private float rmsVal;

    private float dBVal;
    private float pitchVal;

    private float[] samples;
    private float[] spectrum;
    private float sampleRate;

    // Lists of high and low values stored in format dB, Hz 
    private List<Tuple<float, float>> highValues;
    private List<Tuple<float, float>> lowValues;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        highValues = new List<Tuple<float, float>>();
        lowValues = new List<Tuple<float, float>>();

        samples = new float[qSamples];
        spectrum = new float[qSamples];
        sampleRate = AudioSettings.outputSampleRate;
    }
    private void AnalyzeSound()
    {
        // CALCULATING dB

        audioSource.GetOutputData(samples, 0);
        float sumSquares = 0.0f;

        // Sum up the square of all of the samples
        for (int i = 0; i < qSamples; i++)
        {
            sumSquares += Mathf.Pow(samples[i], 2);
        }

        rmsVal = Mathf.Sqrt(sumSquares / qSamples);
        dBVal = 20 * Mathf.Log10(rmsVal / refRmsVal);

        // calculates a dB val clamped between low and high defined points
        dBVal = Mathf.Clamp(dBVal, min_dB, max_dB);
        Debug.Log("dB val: " + dBVal);

        // CALCULATING Hz

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        float maxSpectrumVal = 0.0f;
        int maxSpectrumIndex = 0;
        for (int i = 0; i < qSamples; i++)
        {
            // If you've found a new max value, then assign the max index and spectrum 
            if(spectrum[i] > maxSpectrumVal && spectrum[i] > minSpectrumVal)
            {
                maxSpectrumVal = spectrum[i];
                maxSpectrumIndex = i;
            }
        }

        // interpolating between neighboring values
        float frequencyAtIndex = maxSpectrumIndex;
        if(maxSpectrumIndex > 0 && maxSpectrumIndex < qSamples - 1)
        {
            float dL = spectrum[maxSpectrumIndex - 1] / spectrum[maxSpectrumIndex];
            float dR = spectrum[maxSpectrumIndex + 1] / spectrum[maxSpectrumIndex];
            frequencyAtIndex += 0.5f * (dR * dR - dL * dL);
        }

        // calculate the dominant frequency at that frame
        pitchVal = frequencyAtIndex * (sampleRate / 2) / qSamples;
        Debug.Log("Hz val: " + pitchVal);
    }

    private void CaptureHighSound()
    {
        highValues.Add(new Tuple<float, float>(dBVal, pitchVal));
    }
    private void CaptureLowSound()
    {
        lowValues.Add(new Tuple<float, float>(dBVal, pitchVal));
    }

    private void Update()
    {
        AnalyzeSound();
        MoodValue = (dBVal + Mathf.Abs(min_dB)) / (max_dB + Mathf.Abs(min_dB));

        if (Input.GetKey(KeyCode.UpArrow))
        {
            CaptureHighSound();
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            CaptureLowSound();
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("Low size: " + lowValues.Count);
        Debug.Log("High size: " + highValues.Count);

        StreamWriter lowValueWriter = new StreamWriter(lowValueFilePath, true);
        StreamWriter highValueWriter = new StreamWriter(highValueFilePath, true);

        float avgLowFreq, avgHighFreq;
        float avgLowdB, avgHighdB;
        float freqSum = 0.0f, dBSum = 0.0f;

        // Calculate the average for low values
        foreach (Tuple<float, float> pair in lowValues)
        {
            dBSum += pair.Item1;
            freqSum += pair.Item2;
        }
        avgLowdB = dBSum / lowValues.Count;
        avgLowFreq = freqSum / lowValues.Count;

        Debug.Log("avg low: " + avgLowdB + " " + avgLowFreq);

        // Writing values into files
        lowValueWriter.WriteLine(avgLowdB);
        lowValueWriter.WriteLine(avgLowFreq);

        lowValueWriter.Close();

        AssetDatabase.ImportAsset(lowValueFilePath);

        dBSum = 0.0f;
        freqSum = 0.0f;

        // Calculate the average for high values
        foreach (Tuple<float, float> pair in highValues)
        {
            dBSum += pair.Item1;
            freqSum += pair.Item2;
        }
        avgHighdB = dBSum / highValues.Count;
        avgHighFreq = freqSum / highValues.Count;

        Debug.Log("avg high: " + avgHighdB + " " + avgHighFreq);

        highValueWriter.WriteLine(avgHighdB);
        highValueWriter.WriteLine(avgHighFreq);

        highValueWriter.Close();

        AssetDatabase.ImportAsset(highValueFilePath);
    }

}
