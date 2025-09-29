
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
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
		private const string MemoryMappedFileName = "Local\\MAIRARefactoredTelemetry";
		private const int MaxStringLengthInBytes = 256;

		private const int ExpectedVersion = 4;

		[StructLayout( LayoutKind.Sequential, Pack = 4 )]
		public unsafe struct DataBufferStruct
		{
			public int tickCount;

			public float racingWheelStrength;
			public float racingWheelMaxForce;

			public float racingWheelAutoTorque;

			public int racingWheelAlgorithm;
			public fixed byte racingWheelAlgorithmName[ MaxStringLengthInBytes ];

			public bool racingWheelAlgorithmSoftLimiterIsEnabled;
			public fixed byte racingWheelAlgorithmSoftLimiterName[ MaxStringLengthInBytes ];
			public fixed byte racingWheelAlgorithmSoftLimiterValue[ MaxStringLengthInBytes ];

			public fixed float racingWheelAlgorithmSettings[ 4 ];
			public fixed byte racingWheelAlgorithmSettingNames[ 4 * MaxStringLengthInBytes ];
			public fixed byte racingWheelAlgorithmSettingValues[ 4 * MaxStringLengthInBytes ];

			public float racingWheelOutputTorque;
			public bool racingWheelOutputTorqueIsClipping;

			public bool racingWheelCrashProtectionIsActive;
			public bool racingWheelCurbProtectionIsActive;

			public bool racingWheelFadingIsActive;

			public float steeringEffectsUndersteerEffect;
			public float steeringEffectsOversteerEffect;
			public float steeringEffectsSeatOfPantsEffect;
			public float steeringEffectsSkidSlip;

			public float pedalsClutchFrequency;
			public float pedalsClutchAmplitude;

			public float pedalsBrakeFrequency;
			public float pedalsBrakeAmplitude;

			public float pedalsThrottleFrequency;
			public float pedalsThrottleAmplitude;

			public string GetRacingWheelAlgorithmName()
			{
				fixed ( byte* bytePtr = racingWheelAlgorithmName )
				{
					return ReadString( bytePtr, 0, MaxStringLengthInBytes );
				}
			}

			public string GetRacingWheelAlgorithmSoftLimiterName()
			{
				fixed ( byte* bytePtr = racingWheelAlgorithmSoftLimiterName )
				{
					return ReadString( bytePtr, 0, MaxStringLengthInBytes );
				}
			}

			public string GetRacingWheelAlgorithmSoftLimiterValue()
			{
				fixed ( byte* bytePtr = racingWheelAlgorithmSoftLimiterValue )
				{
					return ReadString( bytePtr, 0, MaxStringLengthInBytes );
				}
			}

			public string GetRacingWheelAlgorithmSettingName( int index )
			{
				if ( index < 0 || index >= 5 ) return string.Empty;

				fixed ( byte* bytePtr = racingWheelAlgorithmSettingNames )
				{
					return ReadString( bytePtr, index, MaxStringLengthInBytes );
				}
			}

			public string GetRacingWheelAlgorithmSettingValue( int index )
			{
				if ( index < 0 || index >= 5 ) return string.Empty;

				fixed ( byte* bytePtr = racingWheelAlgorithmSettingValues )
				{
					return ReadString( bytePtr, index, MaxStringLengthInBytes );
				}
			}

			public static unsafe string ReadString( byte* bytePtr, int index, int capacity )
			{
				if ( bytePtr == null || capacity <= 0 ) return string.Empty;

				var offset = index * capacity;

				var length = 0;

				while ( length < capacity && bytePtr[ offset + length ] != 0 ) length++;

				if ( length == 0 ) return string.Empty;

				var tmp = new byte[ length ];

				Marshal.Copy( (IntPtr) bytePtr + offset, tmp, 0, length );

				return Encoding.UTF8.GetString( tmp );
			}
		}

		[StructLayout( LayoutKind.Sequential, Pack = 4 )]
		public unsafe struct DataStruct
		{
			public int version;
			public int bufferIndex;

			public DataBufferStruct buffer0;
			public DataBufferStruct buffer1;
			public DataBufferStruct buffer2;

			public static ref DataBufferStruct GetDataBuffer( ref DataStruct dataStruct, int index )
			{
				switch ( index )
				{
					case 0: return ref dataStruct.buffer0;
					case 1: return ref dataStruct.buffer1;
					default: return ref dataStruct.buffer2;
				}
			}
		}

		public PluginManager PluginManager { get; set; }

		public ImageSource PictureIcon => this.ToIcon( Properties.Resources.icon.ToBitmap() );

		public string LeftMenuTitle => "MAIRA Refactored data plugin";

		private PluginSettings Settings { get; set; }

		private DataStruct data = new DataStruct();
		private MemoryMappedFile memoryMappedFile = null;
		private MemoryMappedViewAccessor memoryMappedFileViewAccessor = null;

		private bool faulted = false;
		private bool connected = false;

		private int nextMemoryMappedFileAttempt = 0;
		private int nextConnectedCheck = 0;

		private int lastTickCount = 0;

		private ref DataBufferStruct DataBuffer => ref DataStruct.GetDataBuffer( ref data, data.bufferIndex );

		public void Init( PluginManager pluginManager )
		{
			SimHub.Logging.Current.Info( "Starting MAIRA Refactored data plugin" );

			Settings = this.ReadCommonSettings<PluginSettings>("MarvinsAIRARefactoredDataPlugin_Settings.json", () => new PluginSettings());

			unsafe
			{
				this.AttachDelegate( name: "version", valueProvider: () => $"{ExpectedVersion},{this.data.version}" );

				this.AttachDelegate( name: "faulted", valueProvider: () => faulted );
				this.AttachDelegate( name: "connected", valueProvider: () => connected );

				this.AttachDelegate( name: "tickCount", valueProvider: () => DataBuffer.tickCount );

				this.AttachDelegate( name: "racingWheelStrength", valueProvider: () => DataBuffer.racingWheelStrength );
				this.AttachDelegate( name: "racingWheelMaxForce", valueProvider: () => DataBuffer.racingWheelMaxForce );

				this.AttachDelegate( name: "racingWheelAutoTorque", valueProvider: () => DataBuffer.racingWheelAutoTorque );

				this.AttachDelegate( name: "racingWheelAlgorithm", valueProvider: () => DataBuffer.racingWheelAlgorithm );
				this.AttachDelegate( name: "racingWheelAlgorithmName", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmName() );

				this.AttachDelegate( name: "racingWheelAlgorithmSoftLimiterIsEnabled", valueProvider: () => DataBuffer.racingWheelAlgorithmSoftLimiterIsEnabled );
				this.AttachDelegate( name: "racingWheelAlgorithmSoftLimiterName", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSoftLimiterName() );
				this.AttachDelegate( name: "racingWheelAlgorithmSoftLimiterValue", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSoftLimiterValue() );

				this.AttachDelegate( name: "racingWheelAlgorithmSetting0", valueProvider: () => DataBuffer.racingWheelAlgorithmSettings[ 0 ] );
				this.AttachDelegate( name: "racingWheelAlgorithmSetting1", valueProvider: () => DataBuffer.racingWheelAlgorithmSettings[ 1 ] );
				this.AttachDelegate( name: "racingWheelAlgorithmSetting2", valueProvider: () => DataBuffer.racingWheelAlgorithmSettings[ 2 ] );
				this.AttachDelegate( name: "racingWheelAlgorithmSetting3", valueProvider: () => DataBuffer.racingWheelAlgorithmSettings[ 3 ] );

				this.AttachDelegate( name: "racingWheelAlgorithmSettingName0", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSettingName( 0 ) );
				this.AttachDelegate( name: "racingWheelAlgorithmSettingName1", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSettingName( 1 ) );
				this.AttachDelegate( name: "racingWheelAlgorithmSettingName2", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSettingName( 2 ) );
				this.AttachDelegate( name: "racingWheelAlgorithmSettingName3", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSettingName( 3 ) );

				this.AttachDelegate( name: "racingWheelAlgorithmSettingValue0", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSettingValue( 0 ) );
				this.AttachDelegate( name: "racingWheelAlgorithmSettingValue1", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSettingValue( 1 ) );
				this.AttachDelegate( name: "racingWheelAlgorithmSettingValue2", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSettingValue( 2 ) );
				this.AttachDelegate( name: "racingWheelAlgorithmSettingValue3", valueProvider: () => DataBuffer.GetRacingWheelAlgorithmSettingValue( 3 ) );

				this.AttachDelegate( name: "racingWheelOutputTorque", valueProvider: () => DataBuffer.racingWheelOutputTorque );
				this.AttachDelegate( name: "racingWheelOutputTorqueIsClipping", valueProvider: () => DataBuffer.racingWheelOutputTorqueIsClipping );

				this.AttachDelegate( name: "racingWheelCrashProtectionIsActive", valueProvider: () => DataBuffer.racingWheelCrashProtectionIsActive );
				this.AttachDelegate( name: "racingWheelCurbProtectionIsActive", valueProvider: () => DataBuffer.racingWheelCurbProtectionIsActive );

				this.AttachDelegate( name: "racingWheelFadingIsActive", valueProvider: () => DataBuffer.racingWheelFadingIsActive );

				this.AttachDelegate( name: "steeringEffectsUndersteerEffect", valueProvider: () => DataBuffer.steeringEffectsUndersteerEffect );
				this.AttachDelegate( name: "steeringEffectsOversteerEffect", valueProvider: () => DataBuffer.steeringEffectsOversteerEffect );
				this.AttachDelegate( name: "steeringEffectsSeatOfPantsEffect", valueProvider: () => DataBuffer.steeringEffectsSeatOfPantsEffect );
				this.AttachDelegate( name: "steeringEffectsSkidSlip", valueProvider: () => DataBuffer.steeringEffectsSkidSlip );

				this.AttachDelegate( name: "pedalsClutchFrequency", valueProvider: () => DataBuffer.pedalsClutchFrequency );
				this.AttachDelegate( name: "pedalsClutchAmplitude", valueProvider: () => DataBuffer.pedalsClutchAmplitude );

				this.AttachDelegate( name: "pedalsBrakeFrequency", valueProvider: () => DataBuffer.pedalsBrakeFrequency );
				this.AttachDelegate( name: "pedalsBrakeAmplitude", valueProvider: () => DataBuffer.pedalsBrakeAmplitude );

				this.AttachDelegate( name: "pedalsThrottleFrequency", valueProvider: () => DataBuffer.pedalsThrottleFrequency );
				this.AttachDelegate( name: "pedalsThrottleAmplitude", valueProvider: () => DataBuffer.pedalsThrottleAmplitude );

				this.AttachDelegate( name: "overlaysShowInPractice", valueProvider: () => Settings.OverlaysShowInPractice );
				this.AttachDelegate( name: "overlaysShowInQualifying", valueProvider: () => Settings.OverlaysShowInQualifying );
				this.AttachDelegate( name: "overlaysShowInRace", valueProvider: () => Settings.OverlaysShowInRace );
				this.AttachDelegate( name: "overlaysShowInTestDrive", valueProvider: () => Settings.OverlaysShowInTestDrive );
			}
		}

		public void End( PluginManager pluginManager )
		{
			this.SaveCommonSettings("MarvinsAIRARefactoredDataPlugin_Settings.json", Settings);
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

					if ( this.data.version != ExpectedVersion )
					{
						SimHub.Logging.Current.Info( $"MAIRA Refactored data plugin detected an invalid data version {this.data.version}!" );

						throw new Exception( $"Invalid data version {this.data.version}" );
					}

					if ( Environment.TickCount >= nextConnectedCheck )
					{
						connected = DataBuffer.tickCount != lastTickCount;
						lastTickCount = DataBuffer.tickCount;
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
			return new PluginControl( Settings );
		}
	}

	public class PluginSettings
	{
		public bool OverlaysShowInPractice { get; set; } = true;
		public bool OverlaysShowInQualifying { get; set; } = true;
		public bool OverlaysShowInRace { get; set; } = true;
		public bool OverlaysShowInTestDrive { get; set; } = true;
	}
}