
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Text;

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
		private struct DataStruct
		{
			public int version;
			public int tickCount;

			public bool iracingConnected;

			public float racingWheelStrength;
			public float racingWheelMaxForce;

			public float racingWheelOutputTorque;
			public float autoRacingWheelMaxForce;
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

			public int algorithmNameLength;
			public int algorithmParameterNameLength_0;
			public int algorithmParameterValueLength_0;
			public int algorithmParameterNameLength_1;
			public int algorithmParameterValueLength_1;
			public int algorithmParameterNameLength_2;
			public int algorithmParameterValueLength_2;
			public int algorithmParameterNameLength_3;
			public int algorithmParameterValueLength_3;
			public int algorithmParameterNameLength_4;
			public int algorithmParameterValueLength_4;
		}

		private const string MemoryMappedFileName = "Local\\MAIRARefactoredTelemetry";

		public PluginManager PluginManager { get; set; }

		public ImageSource PictureIcon => this.ToIcon( Properties.Resources.icon.ToBitmap() );

		public string LeftMenuTitle => "MAIRA Refactored data plugin";

		private DataStruct data = new DataStruct();
		public string algorithmName;
		public string[] algorithmParameterName = new string[5];
		public string[] algorithmParameterValue = new string[5];

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
			this.AttachDelegate( name: "iracingConnected", valueProvider: () => data.iracingConnected );

			this.AttachDelegate( name: "tickCount", valueProvider: () => data.tickCount );

			this.AttachDelegate( name: "racingWheelStrength", valueProvider: () => data.racingWheelStrength );
			this.AttachDelegate( name: "racingWheelMaxForce", valueProvider: () => data.racingWheelMaxForce );

			this.AttachDelegate( name: "racingWheelOutputTorque", valueProvider: () => data.racingWheelOutputTorque );
			this.AttachDelegate( name: "autoRacingWheelMaxForce", valueProvider: () => data.autoRacingWheelMaxForce );
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

			this.AttachDelegate( name: "algorithmName", valueProvider: () => algorithmName );
			this.AttachDelegate(name: "algorithmParameterName_0", valueProvider: () => algorithmParameterName[0]);
			this.AttachDelegate(name: "algorithmParameterName_1", valueProvider: () => algorithmParameterName[1]);
			this.AttachDelegate(name: "algorithmParameterName_2", valueProvider: () => algorithmParameterName[2]);
			this.AttachDelegate(name: "algorithmParameterName_3", valueProvider: () => algorithmParameterName[3]);
			this.AttachDelegate(name: "algorithmParameterName_4", valueProvider: () => algorithmParameterName[4]);
			this.AttachDelegate(name: "algorithmParameterValue_0", valueProvider: () => algorithmParameterValue[0]);
			this.AttachDelegate(name: "algorithmParameterValue_1", valueProvider: () => algorithmParameterValue[1]);
			this.AttachDelegate(name: "algorithmParameterValue_2", valueProvider: () => algorithmParameterValue[2]);
			this.AttachDelegate(name: "algorithmParameterValue_3", valueProvider: () => algorithmParameterValue[3]);
			this.AttachDelegate(name: "algorithmParameterValue_4", valueProvider: () => algorithmParameterValue[4]);
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

					long filePositionOffset = Marshal.SizeOf( typeof ( DataStruct ) );
					int nameLength = 0;
					int valueLength = 0;

					byte[] stringBytes = new byte[this.data.algorithmNameLength];
					memoryMappedFileViewAccessor?.ReadArray( filePositionOffset, stringBytes, 0, stringBytes.Length );
					algorithmName = Encoding.Unicode.GetString( stringBytes );
					filePositionOffset += stringBytes.Length;

					for ( var i = 0; i < 5; i++ )
					{
						switch ( i )
						{
							case 0:
								nameLength = this.data.algorithmParameterNameLength_0;
								valueLength = this.data.algorithmParameterValueLength_0;
								break;

							case 1:
								nameLength = this.data.algorithmParameterNameLength_1;
								valueLength = this.data.algorithmParameterValueLength_1;
								break;

							case 2:
								nameLength = this.data.algorithmParameterNameLength_2;
								valueLength = this.data.algorithmParameterValueLength_2;
								break;

							case 3:
								nameLength = this.data.algorithmParameterNameLength_3;
								valueLength = this.data.algorithmParameterValueLength_3;
								break;

							case 4:
								nameLength = this.data.algorithmParameterNameLength_4;
								valueLength = this.data.algorithmParameterValueLength_4;
								break;
						}

						if ( nameLength > 0 )
						{
							stringBytes = new byte[nameLength];
							memoryMappedFileViewAccessor?.ReadArray( filePositionOffset, stringBytes, 0, stringBytes.Length );
							algorithmParameterName[i] = Encoding.Unicode.GetString( stringBytes );
							filePositionOffset += stringBytes.Length;
						}
						else
						{
							algorithmParameterName[i] = string.Empty;
						}

						if( valueLength > 0 )
						{
							stringBytes = new byte[valueLength];
							memoryMappedFileViewAccessor?.ReadArray( filePositionOffset, stringBytes, 0, stringBytes.Length );
							algorithmParameterValue[i] = Encoding.Unicode.GetString( stringBytes );
							filePositionOffset += stringBytes.Length;
						}
						else
						{
							algorithmParameterValue[i] = string.Empty;
						}
					}


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