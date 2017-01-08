#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Linq;
using System.Runtime.InteropServices;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Observables;
using DirectShowLib;
using EnterpriseDT.Util.Debug;
using MediaPortal.ExtensionMethods;
using MediaPortal.GUI.Library;
using MediaPortal.Player;

namespace MediaPortal.Mixer
{
  public sealed class Mixer : IDisposable
  {
    #region Events

    public int[] _volumeTable;
    #endregion Events

    #region Methods

    public void Close()
    {
      lock (this)
      {
        if (_handle == IntPtr.Zero)
        {
          return;
        }

        MixerNativeMethods.mixerClose(_handle);

        _handle = IntPtr.Zero;
      }
    }

    public void Dispose()
    {
      if (_audioDefaultDevice != null)
      {
        _audioDefaultDevice.SafeDispose();
      }

      Close();
    }

    public void Open()
    {
      Open(0, false);
    }

    public void Open(int mixerIndex, bool isDigital)
    {
      lock (this)
      {
        _waveVolume = isDigital;
          try
          {
            _audioController = new CoreAudioController();
            _audioDefaultDevice = _audioController.GetDefaultDevice(DeviceType.Playback, Role.Multimedia);
            if (_audioDefaultDevice != null)
            {
              Log.Debug($"Mixer audio device: {_audioDefaultDevice.FullName}");

              // AudioEndpointVolume_OnVolumeNotification
              _audioDefaultDevice.VolumeChanged.Subscribe(x =>
              {
                OnVolumeChange();
              });
              _audioController.AudioDeviceChanged.Subscribe(x =>
              {
                OnDeviceChange();
              });


              _isMuted = _audioDefaultDevice.IsMuted;
              _volume = (int)Math.Round(_audioDefaultDevice.Volume * VolumeMaximum);
            }
          }
          catch (Exception)
          {
            _isMuted = false;
            _volume = VolumeMaximum;
          }
      }
    }

    void OnVolumeChange()
    {
      try
      {
        if (_audioDefaultDevice == null)
          return;

        _isMuted = _audioDefaultDevice.IsMuted;

        // Determine which step in Mediaportal volume table we are.
        // This is needed as external volume control will supply them in a 0-100 format

        int currentVolumePercentage = (int) (_audioDefaultDevice.Volume / 100 * 100);

        if (VolumeHandler.Instance._volumeTable == null)
        {
          VolumeHandler.CreateInstance();
        }

        _volumeTable = VolumeHandler.Instance._volumeTable;

        if (_volumeTable == null)
          return;
        if (_volumeTable.Length == 0)
          return;

        int totalVolumeSteps = _volumeTable.Length;
        decimal volumePercentageDecimal = (decimal)currentVolumePercentage / 100;
        double index = Math.Floor((double)(volumePercentageDecimal * totalVolumeSteps));

        // Make sure we never go out of bounds
        if (index < 0)
          index = 0;

        while (index >= _volumeTable.Length && index != 0)
          index--;

        int volumeStep = _volumeTable[(int)index];
        _volume = volumeStep;
      }
      catch (Exception ex)
      {
        Log.Error($"Error occurred in OnVolumeChange(): {ex}");
      }
    }

    void OnDeviceChange()
    {
      try
      {
        Log.Debug($"Audio device changed detected (old): {_audioDefaultDevice.FullName}");

        var newAudioDevice = _audioController.GetDefaultDevice(DeviceType.Playback, Role.Multimedia);
        if (_audioDefaultDevice.FullName == newAudioDevice.FullName)
          return;

        _audioDefaultDevice = newAudioDevice;
        VolumeHandler.Instance.Volume = VolumeMaximum;
        Log.Debug($"Audio device changed detected (new): {_audioDefaultDevice.FullName}");
      }
      catch (Exception ex)
      {
        Log.Error($"Error occurred in OnDeviceChange(): {ex}");
      }
    }


    #endregion Methods

    #region Properties

    public bool IsMuted
    {
      get { lock (this) return _isMuted; }
      set
      {
        lock (this)
        {
          _audioDefaultDevice?.SetMuteAsync(value);
        }
      }
    }


    public int Volume
    {
      get
      {
        lock (this)
        {
          return _volume;
        }
      }
      set
      {
        _volume = value;
        int volumePercentage = (int)Math.Round((double)(100 * value) / VolumeMaximum);

        lock (this)
        {
          _audioDefaultDevice.SetVolumeAsync(volumePercentage);
          VolumeHandler.Instance.mixer_UpdateVolume();
        }
      }
    }

    public int VolumeMaximum => 65535;

    public int VolumeMinimum => 0;

    #endregion Properties

    #region Fields

    private IntPtr _handle;
    private bool _isMuted;
    private bool _isMutedVolume;
    private int _volume;
    private CoreAudioController _audioController;
    private CoreAudioDevice _audioDefaultDevice;
    private bool _waveVolume;

    #endregion Fields
  }
}