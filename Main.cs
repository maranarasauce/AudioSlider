using BepInEx;
using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AudioSlider
{
    [BepInPlugin("maranara_audio_slider", "Audio Slider", "0.0.1")]
    public class AudioSlider : BaseUnityPlugin
    {
        //Quick n dirty little mod. Not much organization - sorry!
        private void OnEnable()
        {
            Harmony harmony = new Harmony("maranara_audio_slider");
            harmony.PatchAll(typeof(AudioSlider));
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        static AudioMixerGroup musicMixer;
        static AudioMixerGroup goreMixer;
        static bool initialized;

        [HarmonyPatch(typeof(AudioMixerController), "Awake")]
        [HarmonyPostfix]
        static void MixerAwake(AudioMixerController __instance)
        {
            //Store the mixers and mark everything as initialized.
            goreMixer = __instance.goreSound.FindMatchingGroups(string.Empty)[0];
            musicMixer = __instance.musicSound.FindMatchingGroups(string.Empty)[0];

            initialized = true;
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            //If nothing's initialized, return. For sceneLoaded, this could only really occur in the title screen.
            //Though luckily it seems that it gets initialized before that can happen.

            if (!initialized)
                return;

            //Loops through every AudioSource in the current scene. That way we can fix Sources that have PlayOnAwake ticked
            AudioSource[] srcs = Resources.FindObjectsOfTypeAll<AudioSource>();
            foreach (AudioSource src in srcs)
            {
                FixMixer(src);
            }
        }

        [HarmonyPatch(typeof(OptionsMenuToManager), "Start")]
        [HarmonyPostfix]
        static void MenuStart(OptionsMenuToManager __instance)
        {
            //Undo the AudioListener's volume being set
            AudioListener.volume = 1f;
            //Change the MASTER VOLUME text to SFX VOLUME
            __instance.masterVolume.transform.parent.parent.Find("Text").GetComponent<Text>().text = "SFX VOLUME";
        }

        [HarmonyPatch(typeof(OptionsMenuToManager), "MasterVolume")]
        [HarmonyPrefix]
        static bool MasterVolumeSet(OptionsMenuToManager __instance, float stuff)
        {
            //The volume slider is set out of 100, so it's converted to 1 and then stored, and the volume is applied.
            float volume = stuff / 100f;
            MonoSingleton<PrefsManager>.Instance.SetFloat("allVolume", volume);
            goreMixer.audioMixer.SetFloat("allVolume", (double)volume > 0.0 ? Mathf.Log10(volume) * 20f : -80f);
            return false;
        }

        [HarmonyPatch(typeof(AudioSource), "Play", new Type[0], null)]
        [HarmonyPrefix]
        static void Play(AudioSource __instance)
        {
            //Play seems to get called every time an AudioSource is spawned, so this is the perfect time to fix the mixer
            if (!initialized)
                return;

            FixMixer(__instance);
        }

        static void FixMixer(AudioSource src)
        {
            //Just check if it uses music mixer... if not, set it to the SFX mixer

            if (src.outputAudioMixerGroup != null)
            {
                if (src.outputAudioMixerGroup != musicMixer)
                {
                    src.outputAudioMixerGroup = goreMixer;
                }
            }
            else
            {
                src.outputAudioMixerGroup = goreMixer;
            }
        }
    }
}
