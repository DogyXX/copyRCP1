using Godot;
using MQTTnet;
using MQTTnet.Internal;
using OpenCvSharp;
using RoverControlApp.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text;

namespace RoverControlApp.MVVM.ViewModel;
public partial class VelMonitor : Panel
{
    private MqttClient? _mqttClient => MainViewModel.MqttClient;
    const int ITEMS = 4;

	[ExportGroup("Settings")]
	[Export]
	string headStr = "ID ";
	[ExportGroup("Settings")]
	[Export]
	string powerStr = "Power: ";
	[ExportGroup("Settings")]
	[Export]
	string timestr = "Delay: ";
	[ExportGroup("Settings")]
	[Export]
	float SliderMaxVal = 5;
	[ExportGroup("Settings")]
	[Export]
	float SliderMinVal = -5;

	[ExportGroup("NodePaths")]
	[Export]
	NodePath[] headLabs_NodePaths = new NodePath[4];
	[ExportGroup("NodePaths")]
	[Export]
	NodePath[] dataLabs_NodePaths = new NodePath[4];
	[ExportGroup("NodePaths")]
	[Export]
	NodePath[] sliders_NodePaths = new NodePath[4];
	[ExportGroup("NodePaths")]
	[Export]
	NodePath[] timestampLabelNodePaths = new NodePath[ITEMS];
	
	Dictionary<int, int> idSettings = new()
	{
		{ 1, 1 },
		{ 2, 0 },
		{ 3, 3 },
		{ 4, 2 },
		{ 5, 5 },
		{ 6, 4 },
	};

	Label[] headLabs;
	Label[] dataLabs;
	Label[] timestampLabels;
	SliderController[] sliderControllers;
	MqttClasses.WheelControl ?Wheel;

    private bool LenCheck()
	{
		return headLabs_NodePaths.Length == 4 && dataLabs_NodePaths.Length == 4 && sliders_NodePaths.Length == 4 && idSettings.Count == 4;
	}

	public override void _Ready()
	{
		if (!LenCheck())
			throw new Exception("Array lenght missmath!");

		headLabs = new Label[ITEMS];
		dataLabs = new Label[ITEMS];
		timestampLabels = new Label[ITEMS];
		sliderControllers = new SliderController[ITEMS];
		for (int i = 0; i < ITEMS; i++)
		{
			headLabs[i] = GetNode<Label>(headLabs_NodePaths[i]);
			dataLabs[i] = GetNode<Label>(dataLabs_NodePaths[i]);
			timestampLabels[i] = GetNode<Label>(timestampLabelNodePaths[i]);

			var keyOfValue = idSettings.First(kvp => kvp.Value == i).Key;

			headLabs[i].Text = headStr + keyOfValue.ToString();
			dataLabs[i].Text = powerStr + "N/A";
			timestampLabels[i].Text = timestr + "0ms";

			sliderControllers[i] = GetNode<SliderController>(sliders_NodePaths[i]);
			sliderControllers[i].InputMinValue(SliderMinVal);
			sliderControllers[i].InputMaxValue(SliderMaxVal);
		}


    }

public Task MqttSubscriber(string subTopic, MqttApplicationMessage? msg)
{
	if (MainViewModel.Settings?.Settings?.Mqtt.TopicWheelFeedback is null ||
		subTopic != MainViewModel.Settings?.Settings?.Mqtt.TopicWheelFeedback)
		return Task.CompletedTask;

	if (msg is null || msg?.PayloadSegment is null)
	{
		MainViewModel.EventLogger?.LogMessage($"VelMonitor WARNING: Empty payload!");
		return Task.CompletedTask;
	}

	try
	{
		
		Wheel = JsonSerializer.Deserialize<MqttClasses.WheelControl>(msg.ConvertPayloadToString());

		if (Wheel is null)
		{
			MainViewModel.EventLogger?.LogMessage($"VelMonitor WARNING: Failed to deserialize payload!");
			return Task.CompletedTask;
		}
		var vescId = Wheel.VescId;
		
		UpdateVisual(Wheel, vescId);
	}
	catch (Exception e)
	{
		MainViewModel.EventLogger?.LogMessage($"VelMonitor ERROR: Something went wrong: {e.Message}");
	}

	return Task.CompletedTask;
}



public void UpdateVisual(MqttClasses.WheelControl wheelData, int vescId)
{
	try
	{
		var localIdx = idSettings.ContainsKey(vescId) ? idSettings[vescId] : -1;
		if (localIdx != -1)
		{
			dataLabs[localIdx].Text = $"{powerStr} {wheelData.CurrentIn * wheelData.VoltsIn}";
			sliderControllers[localIdx].InputValue((float)(wheelData.CurrentIn * wheelData.VoltsIn));
			long time = ConvertToMMs(wheelData.Timestamp);
			timestampLabels[localIdx].Text = $"{timestr}: {time}";
		}
		else
		{
			MainViewModel.EventLogger?.LogMessage($"VelMonitor WARNING: Unknown VescId: {vescId}");
		}
	}
	catch (Exception e)
	{
		MainViewModel.EventLogger?.LogMessage($"VelMonitor ERROR: Failed to update visual: {e.Message}");
	}
}


	public long ConvertToMMs(long timestamp) { 
		return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp;
	}
}