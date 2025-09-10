
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows.Media;

using GameReaderCommon;

using SimHub.Plugins;

namespace MarvinsAIRARefactoredSimHub
{
	[PluginName( "MAIRA Refactored Data Plugin" )]
	[PluginAuthor( "Marvin Herbold" )]
	[PluginDescription( "Marvin's Awesome iRacing App Data Plugin" )]
	public class MarvinsAIRARefactoredDataPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
	{
		[StructLayout( LayoutKind.Sequential, Pack = 4 )]
		public struct DataStruct
		{
			public int version;
			public int tickCount;

			public float racingWheelStrength;
			public float racingWheelMaxForce;

			public float racingWheelOutputTorque;
			public bool racingWheelOutputTorqueIsClipping;
			public bool racingWheelCrashProtectionIsActive;
			public bool racingWheelCurbProtectionIsActive;
			public bool racingWheelIsFading;

			public float steeringEffectsUndersteerEffect;
			public float steeringEffectsOversteerEffect;
			public float steeringEffectsSkidSlip;

			public float pedalsClutchFrequency;
			public float pedalsClutchAmplitude;

			public float pedalsBrakeFrequency;
			public float pedalsBrakeAmplitude;

			public float pedalsThrottleFrequency;
			public float pedalsThrottleAmplitude;
		}

		private const string MemoryMappedFileName = "Local\\MAIRARefactoredTelemetry";

		public PluginManager PluginManager { get; set; }

		public ImageSource PictureIcon => this.ToIcon( Properties.Resources.icon.ToBitmap() );

		public string LeftMenuTitle => "MAIRA Refactored data plugin";

		private DataStruct data = new DataStruct();
		private MemoryMappedFile memoryMappedFile = null;
		private MemoryMappedViewAccessor memoryMappedFileViewAccessor = null;

		private bool faulted = false;
		private bool connected = false;

		private int nextMemoryMappedFileAttempt = 0;
		private int nextConnectedCheck = 0;

		private int lastTickCount = 0;

		public void Init( PluginManager pluginManager )
		{
			SimHub.Logging.Current.Info( "Starting MAIRA Refactored data plugin" );

			this.AttachDelegate( name: "faulted", valueProvider: () => faulted );
			this.AttachDelegate( name: "connected", valueProvider: () => connected );

			this.AttachDelegate( name: "tickCount", valueProvider: () => data.tickCount );

			this.AttachDelegate( name: "racingWheelStrength", valueProvider: () => data.racingWheelStrength );
			this.AttachDelegate( name: "racingWheelMaxForce", valueProvider: () => data.racingWheelMaxForce );

			this.AttachDelegate( name: "racingWheelOutputTorque", valueProvider: () => data.racingWheelOutputTorque );
			this.AttachDelegate( name: "racingWheelOutputTorqueIsClipping", valueProvider: () => data.racingWheelOutputTorqueIsClipping );
			this.AttachDelegate( name: "racingWheelCrashProtectionIsActive", valueProvider: () => data.racingWheelCrashProtectionIsActive );
			this.AttachDelegate( name: "racingWheelCurbProtectionIsActive", valueProvider: () => data.racingWheelCurbProtectionIsActive );
			this.AttachDelegate( name: "racingWheelIsFading", valueProvider: () => data.racingWheelIsFading );

			this.AttachDelegate( name: "steeringEffectsUndersteerEffect", valueProvider: () => data.steeringEffectsUndersteerEffect );
			this.AttachDelegate( name: "steeringEffectsOversteerEffect", valueProvider: () => data.steeringEffectsOversteerEffect );
			this.AttachDelegate( name: "steeringEffectsSkidSlip", valueProvider: () => data.steeringEffectsSkidSlip );

			this.AttachDelegate( name: "pedalsClutchFrequency", valueProvider: () => data.pedalsClutchFrequency );
			this.AttachDelegate( name: "pedalsClutchAmplitude", valueProvider: () => data.pedalsClutchAmplitude );

			this.AttachDelegate( name: "pedalsBrakeFrequency", valueProvider: () => data.pedalsBrakeFrequency );
			this.AttachDelegate( name: "pedalsBrakeAmplitude", valueProvider: () => data.pedalsBrakeAmplitude );

			this.AttachDelegate( name: "pedalsThrottleFrequency", valueProvider: () => data.pedalsThrottleFrequency );
			this.AttachDelegate( name: "pedalsThrottleAmplitude", valueProvider: () => data.pedalsThrottleAmplitude );
		}

		public void End( PluginManager pluginManager )
		{
		}

		public void DataUpdate( PluginManager pluginManager, ref GameData data )
		{
			if ( faulted )
			{
				return;
			}

			try
			{
				if ( memoryMappedFile == null )
				{
					if ( Environment.TickCount >= nextMemoryMappedFileAttempt )
					{
						try
						{
							memoryMappedFile = MemoryMappedFile.OpenExisting( MemoryMappedFileName );
						}
						catch ( FileNotFoundException )
						{
							nextMemoryMappedFileAttempt = Environment.TickCount + 5000;
						}
					}
				}

				if ( memoryMappedFile != null )
				{
					if ( memoryMappedFileViewAccessor == null )
					{
						memoryMappedFileViewAccessor = memoryMappedFile.CreateViewAccessor();
					}

					memoryMappedFileViewAccessor?.Read( 0, out this.data );

					if ( Environment.TickCount >= nextConnectedCheck )
					{
						connected = this.data.tickCount != lastTickCount;
						lastTickCount = this.data.tickCount;
						nextConnectedCheck = Environment.TickCount + 1000;
					}
				}
			}
			catch
			{
				faulted = true;
			}
		}

		public System.Windows.Controls.Control GetWPFSettingsControl( PluginManager pluginManager )
		{
			return null;
		}
	}
}