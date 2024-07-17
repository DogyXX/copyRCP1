﻿using Godot;
using RoverControlApp.Core;
using System;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace RoverControlApp.MVVM.Model;

public partial class LocalSettings : Node
{
	private sealed class PackedSettings
	{
		public Settings.Camera? Camera { get; set; } = null;
		public Settings.Mqtt? Mqtt { get; set; } = null;
		public Settings.Joystick? Joystick { get; set; } = null;
		public Settings.General? General { get; set; } = null;
	}

	private JsonSerializerOptions serializerOptions = new() { WriteIndented = true };

	private static readonly string _settingsPath = "user://RoverControlAppSettings.json";

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public static LocalSettings Singleton { get; private set; }
#pragma warning restore CS8618 

	[Signal]
	public delegate void CategoryChangedEventHandler(StringName category);

	[Signal]
	public delegate void PropagatedSubcategoryChangedEventHandler(StringName category, StringName subcategory, Variant oldValue, Variant newValue);

	[Signal]
	public delegate void PropagatedPropertyChangedEventHandler(StringName category, StringName property, Variant oldValue, Variant newValue);

	public LocalSettings()
	{
		_camera = new();
		_mqtt = new();
		_joystick = new();
		_general = new();

		if (LoadSettings()) return;

		ForceDefaultSettings();
	}

	public override void _Ready()
	{
		//first ever call to _Ready will be on singletone instance.
		Singleton ??= this;
	}

	public bool LoadSettings()
	{
		try
		{
			using var settingsFileAccess = Godot.FileAccess.Open(_settingsPath, Godot.FileAccess.ModeFlags.Read);

			if (settingsFileAccess is null)
				throw new FieldAccessException(Godot.FileAccess.GetOpenError().ToString());

			var serializedSettings = settingsFileAccess.GetAsText(true);

			var packedSettings = JsonSerializer.Deserialize<PackedSettings>(serializedSettings, serializerOptions);

			if (packedSettings is null)
				throw new DataException("unknown reason");

			Camera = packedSettings.Camera ?? new();
			Mqtt = packedSettings.Mqtt ?? new();
			Joystick = packedSettings.Joystick ?? new();
			General = packedSettings.General ?? new();
		}
		catch (Exception e)
		{
			EventLogger.LogMessage("LocalSettings", EventLogger.LogLevel.Error, $"Loading settings failed:\n\t{e}");
			return false;
		}

		EventLogger.LogMessage("LocalSettings", EventLogger.LogLevel.Info, "Loading settings succeeded");
		return true;
	}

	public bool SaveSettings()
	{
		try
		{
			using var settingsFileAccess = Godot.FileAccess.Open(_settingsPath, Godot.FileAccess.ModeFlags.Write);

			if (settingsFileAccess is null)
				throw new FieldAccessException(Godot.FileAccess.GetOpenError().ToString());

			PackedSettings packedSettings = new()
			{
				Camera = Camera,
				Mqtt = Mqtt,
				Joystick = Joystick,
				General = General
			};

			settingsFileAccess.StoreString(JsonSerializer.Serialize(packedSettings, serializerOptions));
		}
		catch (Exception e)
		{
			EventLogger.LogMessage("LocalSettings", EventLogger.LogLevel.Error, $"Saving settings failed with:\n\t{e}");
			return false;
		}

		EventLogger.LogMessage("LocalSettings", EventLogger.LogLevel.Info, "Saving settings succeeded");
		return true;
	}

	public void ForceDefaultSettings()
	{
		EventLogger.LogMessage("LocalSettings", EventLogger.LogLevel.Info, "Loading default settings");
		Camera = new();
		Mqtt = new();
		Joystick = new();
		General = new();
	}

	private void EmitSignalCategoryChanged(string sectionName)
	{
		EmitSignal(SignalName.CategoryChanged, sectionName);
		EventLogger.LogMessageDebug("LocalSettings", EventLogger.LogLevel.Verbose, $"Section \"{sectionName}\" was overwritten");
	}

	private void PropagateSignal(StringName signal, StringName category, params Variant[] args)
	{
		Variant[] combined = new Variant[args.Length + 1];

		combined[0] = category;
		args.CopyTo(combined, 1);

		EventLogger.LogMessageDebug("LocalSettings", EventLogger.LogLevel.Verbose, $"Field \"{args[0].AsStringName()}\" from \"{combined[0]}\" was changed. Signal propagated to LocalSettings.");
		
		EmitSignal(signal, combined);
	}

	private Action<StringName, Variant, Variant> CreatePropagator(StringName signal, [CallerMemberName] string category = "")
	{
		return (field, oldVal, newVal) => PropagateSignal(signal, category, field, oldVal, newVal);
	}


	[SettingsManagerVisible(customName: "Camera Settings")]
	public Settings.Camera Camera
	{
		get => _camera;
		set
		{
			_camera = value;

			_camera.Connect(
				Settings.Camera.SignalName.SubcategoryChanged,
				Callable.From(CreatePropagator(SignalName.PropagatedSubcategoryChanged)) 
			);
			_camera.Connect(
				Settings.Camera.SignalName.PropertyChanged,
				Callable.From(CreatePropagator(SignalName.PropagatedPropertyChanged))
			);

			EmitSignalCategoryChanged(nameof(Camera));
		}
	}

	[SettingsManagerVisible(customName: "MQTT Settings")]
	public Settings.Mqtt Mqtt
	{
		get => _mqtt;
		set
		{
			_mqtt = value;

			_mqtt.Connect(
				Settings.Mqtt.SignalName.SubcategoryChanged,
				Callable.From(CreatePropagator(SignalName.PropagatedSubcategoryChanged))
			);
			_mqtt.Connect(
				Settings.Mqtt.SignalName.PropertyChanged,
				Callable.From(CreatePropagator(SignalName.PropagatedPropertyChanged))
			);

			EmitSignalCategoryChanged(nameof(Mqtt));
		}
	}

	[SettingsManagerVisible(customName: "Joystick Settings")]
	public Settings.Joystick Joystick
	{
		get => _joystick;
		set
		{
			_joystick = value;

			_joystick.Connect(
				Settings.Joystick.SignalName.SubcategoryChanged,
				Callable.From(CreatePropagator(SignalName.PropagatedSubcategoryChanged))
			);
			_joystick.Connect(
				Settings.Joystick.SignalName.PropertyChanged,
				Callable.From(CreatePropagator(SignalName.PropagatedPropertyChanged))
			);

			EmitSignalCategoryChanged(nameof(Joystick));
		}
	}

	[SettingsManagerVisible(customName: "General Settings")]
	public Settings.General General
	{
		get => _general;
		set
		{
			_general = value;

			_general.Connect(
				Settings.General.SignalName.SubcategoryChanged,
				Callable.From(CreatePropagator(SignalName.PropagatedSubcategoryChanged))
			);
			_general.Connect(
				Settings.General.SignalName.PropertyChanged,
				Callable.From(CreatePropagator(SignalName.PropagatedPropertyChanged))
			);

			EmitSignalCategoryChanged(nameof(General));
		}
	}

	Settings.Camera _camera;
	Settings.Mqtt _mqtt;
	Settings.Joystick _joystick;
	Settings.General _general;
}


