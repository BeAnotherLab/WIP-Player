﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionDisplay : MonoBehaviour{

	[SerializeField] Text modelNameDisplay;
	[SerializeField] Text batteryDisplay;
	[SerializeField] Text fpsDisplay;
	[SerializeField] Text temperatureDisplay;
	[SerializeField] Image uniqueIdColourDisplay;
	[SerializeField] Image statusDisplay;
	[SerializeField] Color pairedColour;
	[SerializeField] Color availableColour;
	[SerializeField] Color unavailableColour;
	[SerializeField] Interpolation lockDisplay;
	[SerializeField] Text textPair;
	[SerializeField] Text textUnpair;
	[SerializeField] Button pairButton;
	[SerializeField] Text textLock;
	[SerializeField] Text textUnlock;
	[SerializeField] Button lockButton;
	[SerializeField] ColorDynamicModifier lockColourModifier;
	[SerializeField] Color lockAvailableColour;
	[SerializeField] Color lockUnavailableColour;
	[SerializeField] Button recenterButton;
	[SerializeField] Button editDeviceNameButton;
	[SerializeField] InputField editDeviceNameField;

	public TCPConnection Connection { get; private set; }

	public int Battery {
		set {
			batteryDisplay.text = value + "%";
		}
	}

	public float FPS {
		set {
			fpsDisplay.text = value + " FPS";
		}
	}

	public int Temperature {
		set {
			if(value == int.MaxValue) {
				//unavailable
				temperatureDisplay.text = "";
			} else {
				temperatureDisplay.text = value + "°";
			}
		}
	}

	string DeviceAlias {
		get {
			string alias = "";
			if(HazePrefs.HasKey("alias-" + Connection.uniqueId)) {
				alias = HazePrefs.GetString("alias-" + Connection.uniqueId);
			}
			if(alias == "")
				return Connection.xrDeviceModel;
			else
				return alias;
		}
		set {
			if(value == "" || value == Connection.xrDeviceModel) {
				if(HazePrefs.HasKey("alias-" + Connection.uniqueId)) {
					HazePrefs.DeleteKey("alias-" + Connection.uniqueId);
				}
			} else {
				HazePrefs.SetString("alias-" + Connection.uniqueId, value);
			}
		}
	}

	List<string> __videosAvailable = new List<string>();
	public List<string> VideosAvailable { get { return __videosAvailable; } }

	public bool IsVideoReady { get; set; }

	bool hasClosedLock = false;


	bool __available = true;
	public bool Available {//True when it's possible for us to connect to this device
		get {
			if(Connection == null || Connection.active == false)
				return false;//this device is not responding anymore... (this means we'll be getting rid of this display soon anyways)
			if(Connection.paired)
				return true;//in any case we're connected so...
			return __available && (Connection.lockedId == SystemInfo.deviceUniqueIdentifier || Connection.lockedId == "free");
		}
		set {
			__available = value;
		}
	}

	bool initialized = false;
	public void Init(TCPConnection connection) {
		if(initialized) {
			Debug.LogError("Cannot reinit a ConnectionDisplay!");
			return;
		}
		initialized = true;
		Connection = connection;
		UpdateDisplay();
		uniqueIdColourDisplay.color = DeviceColour.getDeviceColor(connection.uniqueId);
		modelNameDisplay.text = DeviceAlias;

		editDeviceNameField.gameObject.SetActive(false);
	}

	public void AddAvailableVideo(string videoName) {
		VideosAvailable.Add(videoName);
	}

	public void OnClickCalibrate() {
		if(GuideAdapter.Instance)
			GuideAdapter.Instance.SendCalibrate(Connection);
	}

	public void OnClickLock() {
		if(GuideAdapter.Instance) {
			if(Connection.lockedId == "free") {
				//lock it to us
				GuideAdapter.Instance.SendGuideLock(Connection);
			} else {
				//unlock it
				GuideAdapter.Instance.SendGuideUnlock(Connection);
			}
		}
	}

	public void OnClickPair() {
		if(GuideAdapter.Instance) {
			if(!Connection.paired) {
				Debug.Log("Sending pair");
				GuideAdapter.Instance.SendGuidePair(Connection);
			} else {
				Debug.Log("Sending unpair");
				GuideAdapter.Instance.SendGuideUnpair(Connection);
			}
		}
	}

	public void OnClickLogs() {
		if(GuideAdapter.Instance)
			GuideAdapter.Instance.SendLogsQuery(Connection);
	}

	public void UpdateDisplay() {

		Color statusColor = statusDisplay.color;
		Color uniqueIdColor = uniqueIdColourDisplay.color;

		if(Connection.paired) {
			statusColor = pairedColour;
			textPair.gameObject.SetActive(false);
			textUnpair.gameObject.SetActive(true);
			pairButton.gameObject.SetActive(true);
			lockButton.gameObject.SetActive(true);
			recenterButton.gameObject.SetActive(true);
		}else if(Available) {
			statusColor = availableColour;
			textPair.gameObject.SetActive(true);
			textUnpair.gameObject.SetActive(false);
			lockButton.gameObject.SetActive(false);
			recenterButton.gameObject.SetActive(false);
			if(GuideVideoPlayer.Instance.HasVideoLoaded) {//can't pair with a device while a video is loaded up.
				pairButton.gameObject.SetActive(false);
			} else {
				pairButton.gameObject.SetActive(true);
			}
			StartCoroutine(enableUnlockButtonAfterABit());
		} else {
			statusColor = unavailableColour;
			pairButton.gameObject.SetActive(false);
			lockButton.gameObject.SetActive(false);
			recenterButton.gameObject.SetActive(false);
			StartCoroutine(enableUnlockButtonAfterABit());
		}

		if(Connection.responsive) {
			statusColor.a = 1;
			uniqueIdColor.a = 1;
		} else {
			statusColor.a = 0.3f;
			uniqueIdColor.a = 0.3f;
		}

		statusDisplay.color = statusColor;
		uniqueIdColourDisplay.color = uniqueIdColor;

		//the lock is closed and the device is unlocked:
		if(hasClosedLock && Connection.lockedId == "free") {
			hasClosedLock = false;
			textLock.gameObject.SetActive(true);
			textUnlock.gameObject.SetActive(false);
			lockDisplay.InterpolateBackward();//open lock
			lockColourModifier.DefaultColor = lockUnavailableColour;
		}else if(!hasClosedLock && Connection.lockedId != "free") {
			hasClosedLock = true;
			textLock.gameObject.SetActive(false);
			textUnlock.gameObject.SetActive(true);
			lockDisplay.Interpolate();//close lock
			//Should the lock be green or grey?
			if(Connection.lockedId == SystemInfo.deviceUniqueIdentifier) {
				//green lock
				lockColourModifier.DefaultColor = lockAvailableColour;
			} else {
				//grey lock
				lockColourModifier.DefaultColor = lockUnavailableColour;
			}
		}


		//If we don't have Admin Access, disable lock button and edit device name abilities.
		if(!SettingsAuth.TemporalUnlock) {
			editDeviceNameButton.enabled = false;
			lockButton.gameObject.SetActive(false);
		} else {
			editDeviceNameButton.enabled = true;
		}
	}

	IEnumerator enableUnlockButtonAfterABit() {
		yield return new WaitForSeconds(3);
		if(!Connection.paired) {
			//are we allowed to show the unlock button?
			if(Connection.lockedId != "free") {
				lockButton.gameObject.SetActive(true);
			}
		}
	}

	public void OnClickEditDeviceName() {
		editDeviceNameField.gameObject.SetActive(true);
		editDeviceNameField.text = DeviceAlias;
	}

	public void OnSubmitNewDeviceName() {
		editDeviceNameField.gameObject.SetActive(false);
		DeviceAlias = editDeviceNameField.text;
		modelNameDisplay.text = DeviceAlias;
	}

}