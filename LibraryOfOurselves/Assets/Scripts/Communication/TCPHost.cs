﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public class TCPHost : MonoBehaviour{

	[SerializeField] UDPBroadcaster broadcaster;
	[SerializeField] TCPConnection.DeviceType deviceType = TCPConnection.DeviceType.GUIDE;
	[SerializeField] NewConnectionEvent onNewConnection;
	[SerializeField] MessageReceivedEvent onMessageReception;
	[SerializeField] ConnectionEndEvent onConnectionEnd;

	public static TCPHost Instance { get; private set; }

	TcpListener listener = null;
	bool stop = false;
	List<TCPConnection> users = new List<TCPConnection>();

    async void Start(){
		Instance = this;
		if(broadcaster) {

			string hostName = Dns.GetHostName();
			IPHostEntry hostEntry = await Dns.GetHostEntryAsync(hostName);
			int ipIndex = 0;
			for(int i = 0; i<hostEntry.AddressList.Length; ++i) {
				Debug.Log("Address " + i + " = " + hostEntry.AddressList[i]);
				if(hostEntry.AddressList[i].GetAddressBytes()[0] == (byte)192 && hostEntry.AddressList[i].GetAddressBytes()[1] == (byte)168)
					ipIndex = i;
			}
			IPAddress ip = hostEntry.AddressList[ipIndex];
			
			if(ip.AddressFamily != AddressFamily.InterNetwork) {
				ip = ip.MapToIPv4();
			}
			Debug.Log("Ip: " + ip);

			listener = new TcpListener(ip, 0);
			listener.Start();
			IPEndPoint listenerLocalEndpoint = (IPEndPoint)listener.LocalEndpoint;

			broadcaster.StartBroadcasting(listenerLocalEndpoint.Address.ToString(), listenerLocalEndpoint.Port);
			Debug.Log("Broadcasting ip " + ip.ToString() + " and port " + listenerLocalEndpoint.Port);

			while(!stop) {
				try {
					TcpClient client = await listener.AcceptTcpClientAsync();
					IPEndPoint localEndpoint = (IPEndPoint)client.Client.LocalEndPoint;
					IPEndPoint remoteEndpoint = (IPEndPoint)client.Client.RemoteEndPoint;
					Debug.Log("Accepted connection from " + remoteEndpoint.Address + " (port " + remoteEndpoint.Port + "), from address " + localEndpoint.Address + " (port " + localEndpoint.Port + ")");
					TCPConnection connection = new TCPConnection();
					connection.client = client;

					//wait for identification message:
					List<byte> data = await connection.Receive();
					string channel = data.ReadString();
					if(channel != "identification") {
						Debug.LogWarning("Device at " + remoteEndpoint.Address + " has responded with an illegal channel: " + channel);
					} else {
						connection.deviceType = (TCPConnection.DeviceType)data.ReadByte();
						connection.uniqueId = data.ReadString();
						connection.lockedId = data.ReadString();

						users.Add(connection);

						onNewConnection.Invoke(connection);

						Communicate(connection);
					}

				}catch(SocketException se) {
					Debug.LogWarning("Socket error (" + se.ErrorCode + "), could not accept connection: " + se.ToString());
				}catch(Exception e) {
					Debug.LogWarning("Error, could not accept connection: " + e.ToString());
				}
				
			}
		} else {
			Debug.LogError("No broadcaster...");
		}
    }

	private async void Communicate(TCPConnection connection) {
		while(connection.active) {
			List<byte> data = await connection.Receive();
			if(data.Count > 0) {
				string channel = data.ReadString();
				if(channel == "disconnection") {
					connection.active = false;
				} else {
					//received a message from the host!
					onMessageReception.Invoke(connection, channel, data);
				}
			} else {
				connection.active = false;
			}
		}
		onConnectionEnd.Invoke(connection);
		users.Remove(connection);
	}

	public void BroadcastToPairedDevices(List<byte> data) {
		foreach(TCPConnection conn in users) {
			if(conn.active && conn.paired) {
				conn.Send(data);
			}
		}
	}

	private void OnDestroy() {

		Instance = null;

		foreach(TCPConnection conn in users) {
			if(conn.active) {
				List<byte> data = new List<byte>();
				data.WriteString("disconnection");
				conn.Send(data);
			}
		}

		stop = true;
		if(listener != null) {
			listener.Stop();
		}
	}
}
