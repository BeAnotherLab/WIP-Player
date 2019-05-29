﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class Settings : MonoBehaviour {

	[SerializeField] InputField newPinField;
	[SerializeField] UnityEvent onChangePin;

	public void OnClickChangePin() {
		SettingsAuth.CurrentPIN = newPinField.text.Length > 0 ? newPinField.text : "0000";
		onChangePin.Invoke();
	}

	public void OnChangeLanguage() {
		if(VideoDisplay.expandedDisplay != null)
			VideoDisplay.expandedDisplay.expand();
	}

}