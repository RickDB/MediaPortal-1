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
using System.Threading;
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
          try
          {
            _audioController = new CoreAudioController();
            _audioDefaultDevice = _audioController.GetDefaultDevice(DeviceType.Playback, Role.Multimedia);
            if (_audioDefaultDevice != null)
            {
              Log.Debug($"Mixer audio device: {_audioDefaultDevice.FullName}");

              _audioController.AudioDeviceChanged.Subscribe(x =>
              {
                OnDeviceChange();
              });

              //volume = (int)Math.Round(_audioDefaultDevice.Volume * VolumeMaximum);
            }
          }
          catch (Exception)
          {
            _isMuted = false;
            _volume = VolumeMaximum;
          }
      }
    }

    public void ChangeAudioDevice(string deviceName, bool setToDefault)
    {
      // Execute in thread as device list depending on system might take longer on some systems
      new Thread(() =>
      {
        try
        {
          if (_audioController == null)
            _audioController = new CoreAudioController();

          if (setToDefault)
          {
            _audioDefaultDevice = _audioController.GetDefaultDevice(DeviceType.Playback, Role.Multimedia);
            return;
          }

          var deviceFound = _audioController.GetDevices()
            .FirstOrDefault(device => device.FullName.Trim().ToLowerInvariant() == deviceName.Trim().ToLowerInvariant());

          if (deviceFound != null)
          {
            _audioDefaultDevice = deviceFound;
            Log.Info($"Mixer: changed audio device to : {deviceFound.FullName}");
          }
          else
            Log.Info($"Mixer: ChangeAudioDevice failed because device {deviceName} was not found.");
        }
        catch (Exception ex)
        {
          Log.Error($"Mixer: error occured in ChangeAudioDevice: {ex}");
        }
      }).Start();
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
          _isMuted = value;
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
        lock (this)
        {
          _volume = value;
          int volumePercentage = (int)Math.Round((double)(100 * value) / VolumeMaximum);

          switch (volumePercentage)
          {
            case 0:
              IsMuted = true;
              break;
            default:
              _audioDefaultDevice?.SetVolumeAsync(volumePercentage);
              IsMuted = false;
              break;
          }

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
    private int _volume;
    private CoreAudioController _audioController;
    private CoreAudioDevice _audioDefaultDevice;

    #endregion Fields
  }
}