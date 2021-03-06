﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class StoragePermissionDialog : MonoBehaviour{
#if UNITY_ANDROID

	[SerializeField] UnityEvent onPermissionHasBeenRefused;
	[SerializeField] UnityEvent onPermissionHasBeenAccepted;

	private void Awake() {
		if(VrPlayerBindings.Instance != null) {
			if(!VrPlayerBindings.Instance.isExternalStoragePermissionEnabled()) {
				//attempt to enable it
				Haze.Logger.Log("Requesting storage permission.");
				VrPlayerBindings.Instance.requestExternalStoragePermission(gameObject, "CallbackPermission");
			} else {
				Haze.Logger.Log("Storage permission is enabled.");
			}
		}
	}
	
	public void CallbackPermission(string message) {
		message = message.ToLower();
		if(message.Contains("granted")) {
			//ok!
			onPermissionHasBeenAccepted.Invoke();
		} else {
			Haze.Logger.LogError("Could not grant external storage permission:\n" + message);
			onPermissionHasBeenRefused.Invoke();
		}
	}

#endif
}
