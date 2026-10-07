
using System;
using System.ComponentModel;
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
	public class MarvinsAIRARefactoredDataPlugin : IPlugin, IDataPlugin, IWPFSettingsV2, INotifyPropertyChanged
	{
		private const string MemoryMappedFileName = "Local\\MAIRARefactoredTelemetry";
		private const int MaxStringLengthInBytes = 256;
		private const int MaxGraphNodes = 20;
		private const int MaxGraphNodeParameters = 6;
		private const int ExpectedVersion = 9;

		public int PluginPropertiesVersion => ExpectedVersion;
		
		private int _mairaVersion;

		public int MairaPropertiesVersion
		{
			get => _mairaVersion;

			private set
			{
				if ( _mairaVersion != value )
				{
					_mairaVersion = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( MairaPropertiesVersion ) ) );
                }
			}
		}

		private bool _mairaVersionError;

		public bool MairaVersionError
		{
			get => _mairaVersionError;

			private set
			{
				if ( _mairaVersionError != value )
				{
					_mairaVersionError = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( MairaStatus ) ) );
                }
			}
		}

		[StructLayout( LayoutKind.Sequential, Pack = 4 )]
		public unsafe struct DataBufferStruct
		{
			// header telemetry

			public int tickCount;

			// output telemetry

			public float racingWheelAutoTorque;
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

			// racing wheel settings telemetry

			public float racingWheelStrength;
			public float racingWheelMaxForce;

			// steering effects settings telemetry

			public fixed byte steeringEffectsCalibrationFileName[ MaxStringLengthInBytes ];

			public float steeringEffectsUndersteerMinThreshold;
			public float steeringEffectsUndersteerMaxThreshold;
			public float steeringEffectsUndersteerPedalVibrationMinFrequency;
			public float steeringEffectsUndersteerPedalVibrationMaxFrequency;
			public float steeringEffectsUndersteerPedalVibrationCurve;

			public float steeringEffectsOversteerMinThreshold;
			public float steeringEffectsOversteerMaxThreshold;
			public float steeringEffectsOversteerPedalVibrationMinFrequency;
			public float steeringEffectsOversteerPedalVibrationMaxFrequency;
			public float steeringEffectsOversteerPedalVibrationCurve;

			public float steeringEffectsSeatOfPantsMinThreshold;
			public float steeringEffectsSeatOfPantsMaxThreshold;
			public fixed byte steeringEffectsSeatOfPantsAlgorithm[ MaxStringLengthInBytes ];
			public float steeringEffectsSeatOfPantsPedalVibrationMinFrequency;
			public float steeringEffectsSeatOfPantsPedalVibrationMaxFrequency;
			public float steeringEffectsSeatOfPantsPedalVibrationCurve;

			public bool soundsWheelLockIsPlaying;
			public float soundsWheelLockVolume;
			public bool soundsWheelSpinIsPlaying;
			public float soundsWheelSpinVolume;

			// ffb graph settings telemetry (pinned settings of the active graph, grouped by node)
			// node n is at index n; parameter p of node n is at index ( n * MaxGraphNodeParameters + p )

			public fixed byte graphName[ MaxStringLengthInBytes ];

			public int graphNodeCount;
			public fixed int graphNodeParameterCounts[ MaxGraphNodes ];

			public fixed byte graphNodeNames[ MaxGraphNodes * MaxStringLengthInBytes ];
			public fixed byte graphNodeTypes[ MaxGraphNodes * MaxStringLengthInBytes ];
			public fixed byte graphNodeParameterNames[ MaxGraphNodes * MaxGraphNodeParameters * MaxStringLengthInBytes ];
			public fixed byte graphNodeParameterValues[ MaxGraphNodes * MaxGraphNodeParameters * MaxStringLengthInBytes ];
			public fixed byte graphNodeParameterTexts[ MaxGraphNodes * MaxGraphNodeParameters * MaxStringLengthInBytes ];

			// graph node count getters

			public int GetGraphNodeParameterCount( int nodeIndex )
			{
				if ( nodeIndex < 0 || nodeIndex >= MaxGraphNodes ) return 0;

				return graphNodeParameterCounts[ nodeIndex ];
			}

			// string getters

			public string GetSteeringEffectsCalibrationFileName()
			{
				fixed ( byte* bytePtr = steeringEffectsCalibrationFileName )
				{
					return ReadString( bytePtr, 0, MaxStringLengthInBytes );
				}
			}

			public string GetSteeringEffectsSeatOfPantsAlgorithm()
			{
				fixed ( byte* bytePtr = steeringEffectsSeatOfPantsAlgorithm )
				{
					return ReadString( bytePtr, 0, MaxStringLengthInBytes );
				}
			}

			public string GetGraphName()
			{
				fixed ( byte* bytePtr = graphName )
				{
					return ReadString( bytePtr, 0, MaxStringLengthInBytes );
				}
			}

			public string GetGraphNodeName( int nodeIndex )
			{
				if ( nodeIndex < 0 || nodeIndex >= MaxGraphNodes ) return string.Empty;

				fixed ( byte* bytePtr = graphNodeNames )
				{
					return ReadString( bytePtr, nodeIndex, MaxStringLengthInBytes );
				}
			}

			public string GetGraphNodeType( int nodeIndex )
			{
				if ( nodeIndex < 0 || nodeIndex >= MaxGraphNodes ) return string.Empty;

				fixed ( byte* bytePtr = graphNodeTypes )
				{
					return ReadString( bytePtr, nodeIndex, MaxStringLengthInBytes );
				}
			}

			public string GetGraphNodeParameterName( int nodeIndex, int parameterIndex )
			{
				if ( !IsValidGraphNodeParameter( nodeIndex, parameterIndex ) ) return string.Empty;

				fixed ( byte* bytePtr = graphNodeParameterNames )
				{
					return ReadString( bytePtr, nodeIndex * MaxGraphNodeParameters + parameterIndex, MaxStringLengthInBytes );
				}
			}

			public string GetGraphNodeParameterValue( int nodeIndex, int parameterIndex )
			{
				if ( !IsValidGraphNodeParameter( nodeIndex, parameterIndex ) ) return string.Empty;

				fixed ( byte* bytePtr = graphNodeParameterValues )
				{
					return ReadString( bytePtr, nodeIndex * MaxGraphNodeParameters + parameterIndex, MaxStringLengthInBytes );
				}
			}

			public string GetGraphNodeParameterText( int nodeIndex, int parameterIndex )
			{
				if ( !IsValidGraphNodeParameter( nodeIndex, parameterIndex ) ) return string.Empty;

				fixed ( byte* bytePtr = graphNodeParameterTexts )
				{
					return ReadString( bytePtr, nodeIndex * MaxGraphNodeParameters + parameterIndex, MaxStringLengthInBytes );
				}
			}

			private static bool IsValidGraphNodeParameter( int nodeIndex, int parameterIndex )
			{
				return ( nodeIndex >= 0 ) && ( nodeIndex < MaxGraphNodes ) && ( parameterIndex >= 0 ) && ( parameterIndex < MaxGraphNodeParameters );
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

		// managed layout size (1-byte bools) - what MAIRA sizes its mapping with and what MemoryMappedViewAccessor.Read<T> copies
		private static readonly unsafe int SizeOfData = sizeof( DataStruct );

		private DataStruct data = new DataStruct();
		private MemoryMappedFile memoryMappedFile = null;
		private MemoryMappedViewAccessor memoryMappedFileViewAccessor = null;

        private bool _connected = false;
        
		private bool MairaConnected
		{
			get => _connected;

			set
			{
				if ( _connected != value )
				{
					_connected = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( MairaStatus ) ) );
                }
			}
		}

        private bool _faulted = false;
        
		private bool MairaFaulted
		{
			get => _faulted;

			set
			{
				if ( _faulted != value )
				{
					_faulted = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( MairaStatus ) ) );
                }
            }
		}

        private bool _mairaReadError = false;
        
		private bool MairaReadError
		{
			get => _mairaReadError;

			set
			{
				if ( _mairaReadError != value )
				{
					_mairaReadError = value;
                    PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( MairaStatus ) ) );
                }
            }
		}

        public string MairaStatus
        {
            get
            {
                if ( MairaVersionError && ( MairaPropertiesVersion != 0 ) )
                {
                    return "Unexpected Data Version";
                }
                else if ( MairaFaulted )
                {
                    return "FAULT";
                }
                else if ( MairaReadError )
                {
                    return "MAIRA Read Error";
                }
                else if ( MairaConnected )
                {
                    return "MAIRA Connected";
                }
                else
                {
                    return "MAIRA Not Connected";
                }
            }
        }

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

				this.AttachDelegate( name: "faulted", valueProvider: () => MairaFaulted );
				this.AttachDelegate( name: "connected", valueProvider: () => MairaConnected );

				// header telemetry

				this.AttachDelegate( name: "tickCount", valueProvider: () => DataBuffer.tickCount );

				// output telemetry

				this.AttachDelegate( name: "racingWheelAutoTorque", valueProvider: () => DataBuffer.racingWheelAutoTorque );
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

				// racing wheel settings telemetry

				this.AttachDelegate( name: "racingWheelStrength", valueProvider: () => DataBuffer.racingWheelStrength );
				this.AttachDelegate( name: "racingWheelMaxForce", valueProvider: () => DataBuffer.racingWheelMaxForce );

				// steering effects settings telemetry

				this.AttachDelegate( name: "steeringEffectsCalibrationFileName", valueProvider: () => DataBuffer.GetSteeringEffectsCalibrationFileName() );

				this.AttachDelegate( name: "steeringEffectsUndersteerMinThreshold", valueProvider: () => DataBuffer.steeringEffectsUndersteerMinThreshold );
				this.AttachDelegate( name: "steeringEffectsUndersteerMaxThreshold", valueProvider: () => DataBuffer.steeringEffectsUndersteerMaxThreshold );
				this.AttachDelegate( name: "steeringEffectsUndersteerPedalVibrationMinFrequency", valueProvider: () => DataBuffer.steeringEffectsUndersteerPedalVibrationMinFrequency );
				this.AttachDelegate( name: "steeringEffectsUndersteerPedalVibrationMaxFrequency", valueProvider: () => DataBuffer.steeringEffectsUndersteerPedalVibrationMaxFrequency );
				this.AttachDelegate( name: "steeringEffectsUndersteerPedalVibrationCurve", valueProvider: () => DataBuffer.steeringEffectsUndersteerPedalVibrationCurve );

				this.AttachDelegate( name: "steeringEffectsOversteerMinThreshold", valueProvider: () => DataBuffer.steeringEffectsOversteerMinThreshold );
				this.AttachDelegate( name: "steeringEffectsOversteerMaxThreshold", valueProvider: () => DataBuffer.steeringEffectsOversteerMaxThreshold );
				this.AttachDelegate( name: "steeringEffectsOversteerPedalVibrationMinFrequency", valueProvider: () => DataBuffer.steeringEffectsOversteerPedalVibrationMinFrequency );
				this.AttachDelegate( name: "steeringEffectsOversteerPedalVibrationMaxFrequency", valueProvider: () => DataBuffer.steeringEffectsOversteerPedalVibrationMaxFrequency );
				this.AttachDelegate( name: "steeringEffectsOversteerPedalVibrationCurve", valueProvider: () => DataBuffer.steeringEffectsOversteerPedalVibrationCurve );

				this.AttachDelegate( name: "steeringEffectsSeatOfPantsMinThreshold", valueProvider: () => DataBuffer.steeringEffectsSeatOfPantsMinThreshold );
				this.AttachDelegate( name: "steeringEffectsSeatOfPantsMaxThreshold", valueProvider: () => DataBuffer.steeringEffectsSeatOfPantsMaxThreshold );
				this.AttachDelegate( name: "steeringEffectsSeatOfPantsAlgorithm", valueProvider: () => DataBuffer.GetSteeringEffectsSeatOfPantsAlgorithm() );
				this.AttachDelegate( name: "steeringEffectsSeatOfPantsPedalVibrationMinFrequency", valueProvider: () => DataBuffer.steeringEffectsSeatOfPantsPedalVibrationMinFrequency );
				this.AttachDelegate( name: "steeringEffectsSeatOfPantsPedalVibrationMaxFrequency", valueProvider: () => DataBuffer.steeringEffectsSeatOfPantsPedalVibrationMaxFrequency );
				this.AttachDelegate( name: "steeringEffectsSeatOfPantsPedalVibrationCurve", valueProvider: () => DataBuffer.steeringEffectsSeatOfPantsPedalVibrationCurve );

				this.AttachDelegate( name: "soundsWheelLockIsPlaying", valueProvider: () => DataBuffer.soundsWheelLockIsPlaying );
				this.AttachDelegate( name: "soundsWheelLockVolume", valueProvider: () => DataBuffer.soundsWheelLockVolume );
				this.AttachDelegate( name: "soundsWheelSpinIsPlaying", valueProvider: () => DataBuffer.soundsWheelSpinIsPlaying );
				this.AttachDelegate( name: "soundsWheelSpinVolume", valueProvider: () => DataBuffer.soundsWheelSpinVolume );

				// ffb graph settings telemetry

				this.AttachDelegate( name: "racingWheelGraph.Name", valueProvider: () => DataBuffer.GetGraphName() );
				this.AttachDelegate( name: "racingWheelGraph.NodeCount", valueProvider: () => DataBuffer.graphNodeCount );

				for ( var nodeIndex = 0; nodeIndex < MaxGraphNodes; nodeIndex++ )
				{
					var node = nodeIndex;

					// property names use a 1-based index (racingWheelGraph.Node01 is node 0)

					var nodePropertyName = $"racingWheelGraph.Node{node + 1:00}";

					this.AttachDelegate( name: $"{nodePropertyName}.Name", valueProvider: () => DataBuffer.GetGraphNodeName( node ) );
					this.AttachDelegate( name: $"{nodePropertyName}.ModuleType",valueProvider: () => DataBuffer.GetGraphNodeType( node ) );
					this.AttachDelegate( name: $"{nodePropertyName}.ParameterCount", valueProvider: () => DataBuffer.GetGraphNodeParameterCount( node ) );

					for ( var parameterIndex = 0; parameterIndex < MaxGraphNodeParameters; parameterIndex++ )
					{
						var parameter = parameterIndex;

						var parameterPropertyName = $"{nodePropertyName}.Parameter{parameter + 1:00}";

						this.AttachDelegate( name: $"{parameterPropertyName}.Name", valueProvider: () => DataBuffer.GetGraphNodeParameterName( node, parameter ) );
						this.AttachDelegate( name: $"{parameterPropertyName}.ValueRaw", valueProvider: () => DataBuffer.GetGraphNodeParameterValue( node, parameter ) );
						this.AttachDelegate( name: $"{parameterPropertyName}.ValueFormatted",valueProvider: () => DataBuffer.GetGraphNodeParameterText( node, parameter ) );
					}
				}

				// plugin settings

				this.AttachDelegate( name: "overlaysShowInPractice", valueProvider: () => Settings.OverlaysShowInPractice );
				this.AttachDelegate( name: "overlaysShowInQualifying", valueProvider: () => Settings.OverlaysShowInQualifying );
				this.AttachDelegate( name: "overlaysShowInRace", valueProvider: () => Settings.OverlaysShowInRace );
				this.AttachDelegate( name: "overlaysShowInTestDrive", valueProvider: () => Settings.OverlaysShowInTestDrive );
			}
		}

		public void End( PluginManager pluginManager )
		{
			this.SaveCommonSettings("MarvinsAIRARefactoredDataPlugin_Settings.json", Settings);

			ReleaseMemoryMappedFile();
		}

		public void DataUpdate( PluginManager pluginManager, ref GameData data )
		{
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

						// a mapping smaller than our layout was created by a MAIRA with an older layout - report its version and let go of it
						// (holding it open would also force a newer MAIRA started later to reuse this too-small mapping)

						if ( memoryMappedFileViewAccessor.Capacity < SizeOfData )
						{
							MairaPropertiesVersion = memoryMappedFileViewAccessor.ReadInt32( 0 );
							MairaVersionError = true;

							SimHub.Logging.Current.Info( $"MAIRA Refactored data plugin found a memory mapped file that is too small ({memoryMappedFileViewAccessor.Capacity} < {SizeOfData} bytes) with data version {MairaPropertiesVersion}!" );

							ReleaseMemoryMappedFile();

							nextMemoryMappedFileAttempt = Environment.TickCount + 5000;

							return;
						}
					}

					try
					{
						memoryMappedFileViewAccessor?.Read(0, out this.data);
                        MairaReadError = false;
					}
					catch
					{
                        MairaReadError = true;
					}

					if ( Environment.TickCount >= nextConnectedCheck )
					{
						MairaConnected = DataBuffer.tickCount != lastTickCount;

						if ( MairaConnected )
						{
							MairaPropertiesVersion = this.data.version;

							if ( MairaPropertiesVersion != ExpectedVersion )
							{
                                MairaVersionError = true;

                                SimHub.Logging.Current.Info($"MAIRA Refactored data plugin detected an invalid data version {MairaPropertiesVersion}!");
							}
							else
							{
								MairaVersionError = false;
							}
						}
						else
						{
							MairaPropertiesVersion = 0;

							// MAIRA has stopped writing - let go of the mapping so a restarted MAIRA (possibly with a newer, larger layout) creates a fresh one

							ReleaseMemoryMappedFile();

							nextMemoryMappedFileAttempt = Environment.TickCount + 5000;
						}

                        lastTickCount = DataBuffer.tickCount;
						nextConnectedCheck = Environment.TickCount + 1000;
					}
				}
			}
			catch
			{
                MairaFaulted = true;
			}
		}

		private void ReleaseMemoryMappedFile()
		{
			memoryMappedFileViewAccessor?.Dispose();
			memoryMappedFile?.Dispose();

			memoryMappedFileViewAccessor = null;
			memoryMappedFile = null;
		}

		public System.Windows.Controls.Control GetWPFSettingsControl( PluginManager pluginManager )
		{
			return new PluginControl( this, Settings );
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}

	public class PluginSettings
	{
		public bool OverlaysShowInPractice { get; set; } = true;
		public bool OverlaysShowInQualifying { get; set; } = true;
		public bool OverlaysShowInRace { get; set; } = true;
		public bool OverlaysShowInTestDrive { get; set; } = true;
	}
}
