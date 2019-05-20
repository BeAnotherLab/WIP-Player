﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading.Tasks;

public class VRAdapter : MonoBehaviour{

	[SerializeField] float sendStatusEvery = 0.75f;
	
	TCPConnection currentlyPaired = null;
	string logs = "";
	bool sendStatus = false;

	void OnEnable() {
		Application.logMessageReceived += HandleLog;
	}

	void OnDisable() {
		Application.logMessageReceived -= HandleLog;
	}

	void HandleLog(string message, string stackTrace, LogType type) {
		if(!enabled) return;

		if(logs.Length > 0) logs += "\n";
		switch(type) {
			case LogType.Error:
				logs += "[Error]\t";
				break;
			case LogType.Assert:
				logs += "[Assert]\t";
				break;
			case LogType.Warning:
				logs += "[Warning]\t";
				break;
			case LogType.Log:
				logs += "[Log]\t";
				break;
			case LogType.Exception:
				logs += "[Exception]\t";
				break;
			default:
				logs += "[???]\t";
				break;
		}
		logs += message;
	}


	private void OnDestroy() {
		sendStatus = false;
	}


	public void OnNewConnection(TCPConnection connection) {
		if(currentlyPaired == null && TCPClient.Instance && connection.uniqueId == TCPClient.Instance.LockedId) {
			//we're locked to this one so let's immediately request connection
			SendAutopair(connection);
		}
		if(!sendStatus)
			StartSendingStatusPeriodically();
	}

	public void OnConnectionEnd(TCPConnection connection) {
		if(connection == currentlyPaired) {
			connection.paired = false;
			currentlyPaired = null;
			SendPairConfirm();//advertise as available to all guides
		}
		connection.active = false;
	}

	public void OnReceiveGuideLock(TCPConnection connection) {
		if(currentlyPaired != null || currentlyPaired == connection) {
			if(TCPClient.Instance)
				TCPClient.Instance.LockedId = connection.uniqueId;
			Debug.Log("Locking to " + connection);
		} else {
			Debug.LogWarning("Cannot lock " + connection + ": currently paired with " + currentlyPaired);
		}
	}

	public void OnReceiveGuideUnlock(TCPConnection connection) {
		if(TCPClient.Instance)
			TCPClient.Instance.LockedId = "free";
		Debug.Log("Unlocking.");
	}

	public async void SendAutopair(TCPConnection connection) {
		if(currentlyPaired == null && TCPClient.Instance && connection.uniqueId == TCPClient.Instance.LockedId) {
			List<byte> data = new List<byte>();
			data.WriteString("autopair");
			Debug.Log("Sending autopair request");
			await connection.Send(data);
		} else {
			Debug.LogWarning("Not allowed to send autopair to " + connection + "...");
		}
	}

	public void OnReceiveGuidePair(TCPConnection connection) {
		if(currentlyPaired == null && TCPClient.Instance && (TCPClient.Instance.LockedId == "free" || TCPClient.Instance.LockedId == connection.uniqueId)) {
			currentlyPaired = connection;
			connection.paired = true;
			Debug.Log("Paired to " + connection);
			SendPairConfirm();
		} else {
			Debug.LogWarning("Cannot pair to " + connection);
		}
	}

	public void OnReceiveGuideUnpair(TCPConnection connection) {
		if(currentlyPaired != null && connection == currentlyPaired) {
			currentlyPaired = null;
			connection.paired = false;
			Debug.Log("Unpaired.");
			SendPairConfirm();
		} else {
			Debug.LogWarning("Cannot unpair.");
		}
	}

	public void SendPairConfirm() {
		List<byte> data = new List<byte>();
		data.WriteString("pair-confirm");
		data.WriteString(currentlyPaired != null ? currentlyPaired.uniqueId : "0");
		if(TCPClient.Instance)
			TCPClient.Instance.BroadcastToAllGuides(data);
		if(currentlyPaired != null) SendIsEmpty();
	}

	public void OnReceiveLogsQuery(TCPConnection connection) {
		if(connection == currentlyPaired) {
			SendLogs();
		}
	}

	public async void SendLogs() {
		if(currentlyPaired != null) {
			List<byte> data = new List<byte>();
			data.WriteString("logs");
			data.WriteString(logs);
			logs = "";
			await currentlyPaired.Send(data);
		}
	}

	public void OnReceiveHasVideo(TCPConnection connection, string videoName) {
		if(connection != null && connection == currentlyPaired) {
			if(VRVideoPlayer.IsVideoAvailable(videoName)) {
				SendHasVideoResponse(videoName);
			}
		}
	}

	public async void SendHasVideoResponse(string videoName) {
		if(currentlyPaired != null) {
			List<byte> data = new List<byte>();
			data.WriteString("has-video-response");
			data.WriteString(videoName);
			await currentlyPaired.Send(data);
		}
	}

	public async void SendIsEmpty() {
		if(currentlyPaired != null) {
			List<byte> data = new List<byte>();
			data.WriteString("is-empty");
			await currentlyPaired.Send(data);
		}
	}

	public async void OnReceiveLoadVideo(TCPConnection connection, string videoName) {
		if(currentlyPaired != null && connection == currentlyPaired) {
			if(VRVideoPlayer.Instance) {
				VRVideoPlayer.VideoLoadingResponse response = await VRVideoPlayer.Instance.LoadVideo(videoName);
				SendLoadVideoResponse(response.ok, response.errorMessage);
			}
		}
	}

	public async void SendLoadVideoResponse(bool ok, string errorMessage) {
		if(currentlyPaired != null) {
			List<byte> data = new List<byte>();
			data.WriteString("load-video-response");
			data.WriteBool(ok);
			data.WriteString(errorMessage);
			await currentlyPaired.Send(data);
		}
	}

	public void OnReceivePlayVideo(TCPConnection connection, DateTime timestamp) {
		if(currentlyPaired != null && connection == currentlyPaired) {
			if(VRVideoPlayer.Instance) {
				VRVideoPlayer.Instance.PlayVideo(timestamp);
			}
		}
	}

	public void OnReceivePauseVideo(TCPConnection connection, DateTime unused, double videoTime) {
		if(currentlyPaired != null && connection == currentlyPaired) {
			if(VRVideoPlayer.Instance) {
				VRVideoPlayer.Instance.PauseVideo(videoTime);
			}
		}
	}

	public async void OnReceiveStopVideo(TCPConnection connection) {
		if(currentlyPaired != null && connection == currentlyPaired) {
			if(VRVideoPlayer.Instance) {
				await VRVideoPlayer.Instance.StopVideo();
				SendIsEmpty();
			}
		}
	}

	public void OnReceiveSync(TCPConnection connection, DateTime timestamp, double videoTime) {
		if(currentlyPaired != null && connection == currentlyPaired) {
			if(VRVideoPlayer.Instance) {
				VRVideoPlayer.Instance.Sync(timestamp, videoTime);
			}
		}
	}

	public void OnReceiveCalibrate(TCPConnection connection) {
		if(currentlyPaired != null && connection == currentlyPaired) {
			if(VRVideoPlayer.Instance)
				VRVideoPlayer.Instance.Recenter();
		}
	}

	public async void OnReceiveStartChoice(TCPConnection connection, string choice1, string choice2) {
		if(currentlyPaired != null && connection == currentlyPaired) {
			//TODO: display choices, then send SelectOption once an option has been chosen
			Debug.Log("Dummy: displays choices \'" + choice1 + "\' and \'" + choice2 + "\'");
		}
	}

	public async void SendSelectOption(byte option) {
		if(currentlyPaired != null) {
			List<byte> data = new List<byte>();
			data.WriteString("select-option");
			data.WriteByte(option);
			await currentlyPaired.Send(data);
		}
	}

	public void SendStatus(int battery, float fps, int temperature) {
		List<byte> data = new List<byte>();
		data.WriteString("status");
		data.WriteByte((byte)battery);
		data.WriteShort((short)(fps * 10));
		data.WriteByte((byte)temperature);
		if(TCPClient.Instance)
			TCPClient.Instance.BroadcastToAllGuides(data);
	}

	async void StartSendingStatusPeriodically() {
		sendStatus = true;
		while(sendStatus && TCPClient.Instance && TCPClient.Instance.HasAtLeastOneActiveConnection && Status.Instance) {

			SendStatus(Status.Instance.Battery, Status.Instance.FPS, Status.Instance.Temperature);

			await Task.Delay((int)(sendStatusEvery * 1000));
		}
		sendStatus = false;
	}

}
