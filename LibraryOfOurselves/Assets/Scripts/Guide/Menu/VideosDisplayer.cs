﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using UnityEngine.Events;

public class VideosDisplayer : MonoBehaviour {

	[SerializeField] GameObject videoShelfPrefab;
	[SerializeField] GameObject videoDisplayPrefab;
	[SerializeField] UnityEvent onFoundOneVideo;

	[Serializable]
	public class VideoSettings {
		public bool is360 = false;
		public string description = "";
		public string objectsNeeded = "";
		public Vector3[] deltaAngles = new Vector3[0];

		public override string ToString() {
			return "[VideoSettings: is360=" + is360 + " | description=" + description + " | objectsNeeded=" + objectsNeeded + " | deltaAngles=" + deltaAngles + "]";
		}
	}

	public static VideosDisplayer Instance { get; private set; }


	List<VideoDisplay> displayedVideos = new List<VideoDisplay>();
	GameObject lastVideoShelf = null;

	public void AddVideo(string path) {
		Debug.Log("Adding video file: " + path + "...");
		try {
			//Extract filename
			string[] split = path.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			string directory = "";
			for(int i = 0; i < split.Length - 1; ++i)
				directory += split[i] + "/";
			split = split[split.Length - 1].Split(new string[] { ".mp4" }, StringSplitOptions.RemoveEmptyEntries);
			string filename = "";
			foreach(string f in split) {
				filename += f + ".";
			}
			filename = filename.Substring(0, filename.Length - 1);

			//Is it a guide?
			split = filename.Split(new string[] { "Guide", "guide", "GUIDE" }, StringSplitOptions.None);
			if(split.Length > 1) {
				//ok! this is a guide video.
				string videoName = "";
				foreach(string f in split) {
					videoName += f;
				}

				//Try to find the associated settings file
				string settingsPath = directory + videoName + "_Settings.json";
				VideoSettings settings;
				if(File.Exists(settingsPath)) {
					string json = File.ReadAllText(settingsPath);
					settings = JsonUtility.FromJson<VideoSettings>(json);
				} else {
					//try to find it in the persistent data path instead of the sd card root then maybe?
					settingsPath = Application.persistentDataPath + videoName + "_Settings.json";
					if(File.Exists(settingsPath)) {
						string json = File.ReadAllText(settingsPath);
						settings = JsonUtility.FromJson<VideoSettings>(json);
					} else {
						//no settings for this video yet.
						settings = new VideoSettings();
						Debug.Log("Saving settings...");
						SaveVideoSettings(path, videoName, settings);
					}
				}

				Debug.Log("Displaying video: " + videoName);

				if(lastVideoShelf == null || lastVideoShelf.transform.GetChild(lastVideoShelf.transform.childCount - 1).childCount >= 3) {
					lastVideoShelf = Instantiate(videoShelfPrefab, transform);//add a shelf
				}
				displayedVideos.Add(Instantiate(videoDisplayPrefab, lastVideoShelf.transform.GetChild(lastVideoShelf.transform.childCount - 1)).GetComponent<VideoDisplay>().Init(path, videoName, settings));

				if(displayedVideos.Count == 1)
					onFoundOneVideo.Invoke();

			} else {
				//Not a guide. ignore it.
			}
		}catch(Exception e) {
			//could silently ignore, probably
			Debug.LogWarning("Video " + path + " cannot be added: " + e);
		}
	}

	public void SaveVideoSettings(string videoPath, string videoName, VideoSettings settings) {
#if UNITY_ANDROID && !UNITY_EDITOR
		//save directory is persistent data path
		string directory = Application.persistentDataPath;
#else
		//save directory is sd card root
		string[] split = videoPath.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
		string directory = "";
		for(int i = 0; i<split.Length-1; ++i)
			directory += split[i] + "/";
#endif

		//Save settings to json file
		string json = JsonUtility.ToJson(settings);
		File.WriteAllText(directory + videoName + "_Settings.json", json);
	}

	public void OnPairConnection(TCPConnection connection) {
		foreach(VideoDisplay videoDisplay in displayedVideos) {
			GuideAdapter adapter = GuideAdapter.Instance;
			if(adapter) {
				adapter.SendHasVideo(connection, videoDisplay.VideoName);
			}
		}
	}

	private void Start() {
		Instance = this;
	}

	private void OnDestroy() {
		Instance = null;
	}

	private void Update() {
		//check whether we have all the videos on each device connected to us
		foreach(VideoDisplay videoDisplay in displayedVideos) {
			string videoName = videoDisplay.VideoName;
			ConnectionsDisplayer cd = ConnectionsDisplayer.Instance;
			bool allConnectedDevicesHaveIt = true;
			int numberOfConnectedDevices = 0;
			if(cd != null) {
				foreach(ConnectionsDisplayer.DisplayedConnectionHandle handle in cd.Handles) {
					if(handle.connection.paired) {
						++numberOfConnectedDevices;
						if(!handle.display.VideosAvailable.Contains(videoName)) {
							allConnectedDevicesHaveIt = false;
							if(videoDisplay == VideoDisplay.expandedDisplay)
								videoDisplay.contract();//no longer available.
							break;
						}
					}
				}
			}
			videoDisplay.Available = numberOfConnectedDevices > 0 && allConnectedDevicesHaveIt;
		}
	}

}
