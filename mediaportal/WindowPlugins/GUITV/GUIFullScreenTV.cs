using System;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using MediaPortal.GUI.Library;
using MediaPortal.Util;
using MediaPortal.Dialogs;
using MediaPortal.Player;
using MediaPortal.TV.Recording;
using MediaPortal.TV.Database;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Direct3D = Microsoft.DirectX.Direct3D;

namespace MediaPortal.GUI.TV
{
	/// <summary>
	/// 
	/// </summary>
	public class GUIFullScreenTV : GUIWindow
	{
		bool				m_bLastStatusOSD=false;
		bool				m_bLastMSNChatVisible=false;
		bool				m_bShowInfo=false;
		bool				m_bShowStep=false;
		bool				m_bShowStatus=false;
		bool				m_bShowGroup=false;
		DateTime		m_dwTimeStatusShowTime=DateTime.Now;
		GUITVZAPOSD	m_zapWindow=null;
		GUITVOSD		m_osdWindow=null;
		GUITVMSNOSD	m_msnWindow=null;
		DateTime		m_dwOSDTimeOut;
		DateTime		m_dwZapTimer;
		DateTime		m_dwGroupZapTimer;
//		string			m_sZapChannel;
//		long				m_iZapDelay;
		bool				m_bOSDVisible=false;
		bool				m_bZapOSDVisible=false;
		bool				m_bLastZapOSDVisible=false;
		bool				m_bMSNChatVisible=false;
		bool				m_bUpdate=false;
		bool				m_bShowInput=false;
		bool				m_bLastStatus=false;
		FormOSD			m_form=null;        
		long				m_iMaxTimeOSDOnscreen;
		long				m_iZapTimeOut;
		DateTime		m_UpdateTimer=DateTime.Now;
		bool				m_bLastPause=false;
		int					m_iLastSpeed=1;
		bool				m_bClear=false;
		DateTime		m_timeKeyPressed=DateTime.Now;
		string			m_strChannel="";
		bool				m_bDialogVisible=false;
		bool				m_bLastDialogVisible=false;
		bool				m_bMSNChatPopup=false;
		GUIDialogMenu dlg;
		
		// Message box
		bool				m_bMsgBoxVisible=false;
		DateTime		m_dwMsgTimer=DateTime.Now;
		int					m_iMsgBoxTimeout=0;


    enum Control 
		{
			BLUE_BAR    =0
		, MSG_BOX = 2
		, MSG_BOX_LABEL1 = 3
		, MSG_BOX_LABEL2 = 4
		, MSG_BOX_LABEL3 = 5
		, MSG_BOX_LABEL4 = 6
		, LABEL_ROW1 =10
		, LABEL_ROW2 =11
		, LABEL_ROW3 =12
		, IMG_PAUSE     =16
		, IMG_2X	      =17
		, IMG_4X	      =18
		, IMG_8X		  =19
		, IMG_16X       =20
		, IMG_32X       =21

		, IMG_MIN2X	      =23
		, IMG_MIN4X	      =24
		, IMG_MIN8X		    =25
		, IMG_MIN16X       =26
		, IMG_MIN32X       =27
		, LABEL_CURRENT_TIME =22
		, OSD_VIDEOPROGRESS=100
		, REC_LOGO=39
		};

		ArrayList m_channels = new ArrayList();
		public GUIFullScreenTV()
		{
			GetID=(int)GUIWindow.Window.WINDOW_TVFULLSCREEN;
		}
    
		public override bool Init()
		{
			return Load (GUIGraphicsContext.Skin+@"\mytvFullScreen.xml");
		}

		void LoadSettings()
		{
			using(MediaPortal.Profile.Xml   xmlreader=new MediaPortal.Profile.Xml("MediaPortal.xml"))
			{
				m_bMSNChatPopup = (xmlreader.GetValueAsInt("MSNmessenger", "popupwindow", 0) == 1);
				m_iMaxTimeOSDOnscreen=1000*xmlreader.GetValueAsInt("movieplayer","osdtimeout",5);
//				m_iZapDelay = 1000*xmlreader.GetValueAsInt("movieplayer","zapdelay",2);
				m_iZapTimeOut = 1000*xmlreader.GetValueAsInt("movieplayer","zaptimeout",5);
				string strValue=xmlreader.GetValueAsString("mytv","defaultar","normal");
				if (strValue.Equals("zoom")) GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Zoom;
				if (strValue.Equals("stretch")) GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Stretch;
				if (strValue.Equals("normal")) GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Normal;
				if (strValue.Equals("original")) GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Original;
				if (strValue.Equals("letterbox")) GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.LetterBox43;
				if (strValue.Equals("panscan")) GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.PanScan43;
			}

		}

//		public string ZapChannel
//		{
//			set
//			{
//				m_sZapChannel = value;
//			}
//			get
//			{
//				return m_sZapChannel;
//			}
//		}
		void SaveSettings()
		{
			using (MediaPortal.Profile.Xml xmlwriter = new MediaPortal.Profile.Xml("MediaPortal.xml"))
			{
				switch (GUIGraphicsContext.ARType)
				{
					case MediaPortal.GUI.Library.Geometry.Type.Zoom:
					xmlwriter.SetValue("mytv","defaultar","zoom");
					break;

					case MediaPortal.GUI.Library.Geometry.Type.Stretch:
					xmlwriter.SetValue("mytv","defaultar","stretch");
					break;

					case MediaPortal.GUI.Library.Geometry.Type.Normal:
					xmlwriter.SetValue("mytv","defaultar","normal");
					break;

					case MediaPortal.GUI.Library.Geometry.Type.Original:
					xmlwriter.SetValue("mytv","defaultar","original");
					break;
					case MediaPortal.GUI.Library.Geometry.Type.LetterBox43:
					xmlwriter.SetValue("mytv","defaultar","letterbox");
					break;

					case MediaPortal.GUI.Library.Geometry.Type.PanScan43:
					xmlwriter.SetValue("mytv","defaultar","panscan");
					break;
				}
			}
		}
		public override void OnAction(Action action)
		{
			if (action.wID==Action.ActionType.ACTION_MOUSE_CLICK && action.MouseButton == MouseButtons.Right)
			{
				// switch back to the menu
				m_bOSDVisible=false;
				m_bMSNChatVisible=false;
				GUIGraphicsContext.IsFullScreenVideo=false;
				GUIWindowManager.ShowPreviousWindow();
				return;
			}

			if (m_bOSDVisible)
			{
				if (((action.wID == Action.ActionType.ACTION_SHOW_OSD) || (action.wID == Action.ActionType.ACTION_SHOW_GUI)) && !m_osdWindow.SubMenuVisible) // hide the OSD
				{
					lock(this)
					{ 
						GUIMessage msg= new GUIMessage (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_osdWindow.GetID,0,0,0,0,null);
						m_osdWindow.OnMessage(msg);	// Send a de-init msg to the OSD
						m_bOSDVisible=false;
						m_bUpdate=true;
					}
				}
				else
				{
					m_dwOSDTimeOut=DateTime.Now;
					if (action.wID==Action.ActionType.ACTION_MOUSE_MOVE || action.wID==Action.ActionType.ACTION_MOUSE_CLICK)
					{
						int x=(int)action.fAmount1;
						int y=(int)action.fAmount2;
						if (!GUIGraphicsContext.MouseSupport)
						{
							m_osdWindow.OnAction(action);	// route keys to OSD window
							m_bUpdate=true;
							return;
						}
						else
						{
							if ( m_osdWindow.InWindow(x,y))
							{
								m_osdWindow.OnAction(action);	// route keys to OSD window

								if (m_bZapOSDVisible)
								{
									GUIMessage msg= new GUIMessage (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_zapWindow.GetID,0,0,0,0,null);
									m_zapWindow.OnMessage(msg);
									m_bZapOSDVisible=false;
								}
								m_bUpdate=true;
								return;
							}
							else
							{
								GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_osdWindow.GetID,0,0,0,0,null);
								m_osdWindow.OnMessage(msg);	// Send a de-init msg to the OSD
								m_bOSDVisible=false;
								m_bUpdate=true;
							}
						}
					}
					Action newAction=new Action();
					if (action.wID != Action.ActionType.ACTION_KEY_PRESSED && ActionTranslator.GetAction((int)GUIWindow.Window.WINDOW_TVOSD,action.m_key,ref newAction))
					{
						m_osdWindow.OnAction(newAction);	// route keys to OSD window
						m_bUpdate=true;
					}
					else
					{
						// route unhandled actions to OSD window
						if (!m_osdWindow.SubMenuVisible)
						{
							m_osdWindow.OnAction(action);	
							m_bUpdate=true;
						}
					}
				}
				return;
			}
			else if (m_bMSNChatVisible)
			{
				if (((action.wID == Action.ActionType.ACTION_SHOW_OSD) || (action.wID == Action.ActionType.ACTION_SHOW_GUI))) // hide the OSD
				{
					lock(this)
					{ 
						GUIMessage msg= new GUIMessage (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_msnWindow.GetID,0,0,0,0,null);
						m_msnWindow.OnMessage(msg);	// Send a de-init msg to the OSD
						m_bMSNChatVisible=false;
						m_bUpdate=true;
					}
					return;
				}
				if (action.wID == Action.ActionType.ACTION_KEY_PRESSED)
				{
					m_msnWindow.OnAction(action);
					m_bUpdate=true;
					return;
				}				
			}

			else if (action.wID==Action.ActionType.ACTION_MOUSE_MOVE && GUIGraphicsContext.MouseSupport)
			{
				int y =(int)action.fAmount2;
				if (y > GUIGraphicsContext.Height-100)
				{
					m_dwOSDTimeOut=DateTime.Now;
					GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_INIT,m_osdWindow.GetID,0,0,0,0,null);
					m_osdWindow.OnMessage(msg);	// Send an init msg to the OSD
					m_bOSDVisible=true;
					m_bUpdate=true;
				}
			}
			else if (m_bZapOSDVisible)
			{
				if ((action.wID==Action.ActionType.ACTION_SHOW_GUI) || (action.wID==Action.ActionType.ACTION_SHOW_OSD))
				{
					GUIMessage msg= new GUIMessage (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_zapWindow.GetID,0,0,0,0,null);
					m_zapWindow.OnMessage(msg);
					m_bZapOSDVisible=false;
					m_bUpdate=true;
				}
			}
			switch (action.wID)
			{
				case Action.ActionType.ACTION_SELECT_ITEM:
				{
					GUITVHome.OnLastViewedChannel();
				}
					break;

				case Action.ActionType.ACTION_SHOW_INFO:
				{
					m_dwOSDTimeOut=DateTime.Now;
					GUIMessage msg= new GUIMessage (GUIMessage.MessageType.GUI_MSG_WINDOW_INIT,m_zapWindow.GetID,0,0,0,0,null);
					m_zapWindow.OnMessage(msg);
					Log.Write("ZAP OSD:ON");
					m_bUpdate=true;
					m_bZapOSDVisible=true;
					m_dwZapTimer=DateTime.Now;

				}
					break;
				case Action.ActionType.ACTION_SHOW_MSN_OSD:
					if (m_bMSNChatPopup)
					{
						Log.Write("MSN CHAT:ON");     
						m_bUpdate=true;  
						m_bMSNChatVisible=true;
						m_msnWindow.DoModal( GetID, null );
						m_bMSNChatVisible=false;
					}
					break;

				case Action.ActionType.ACTION_ASPECT_RATIO:
				{
					m_bShowStatus=true;
					m_dwTimeStatusShowTime=DateTime.Now;
					switch (GUIGraphicsContext.ARType)
					{
						case MediaPortal.GUI.Library.Geometry.Type.Zoom:
							GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Stretch;
							break;

						case MediaPortal.GUI.Library.Geometry.Type.Stretch:
							GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Normal;
							break;

						case MediaPortal.GUI.Library.Geometry.Type.Normal:
							GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Original;
							break;

						case MediaPortal.GUI.Library.Geometry.Type.Original:
							GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.LetterBox43;
							break;

						case MediaPortal.GUI.Library.Geometry.Type.LetterBox43:
							GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.PanScan43;
							break;
      
						case MediaPortal.GUI.Library.Geometry.Type.PanScan43:
							GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Zoom;
							break;
					}
					m_bUpdate=true;
					SaveSettings();
				}
					break;

				case Action.ActionType.ACTION_PAGE_UP:
					OnPageUp();
					break;
        
				case Action.ActionType.ACTION_PAGE_DOWN:
					OnPageDown();
					break;

				case Action.ActionType.ACTION_KEY_PRESSED:
				{
					if ((action.m_key!=null) && (!m_bMSNChatVisible))
						OnKeyCode((char)action.m_key.KeyChar);

					HideControl(GetID, (int)Control.MSG_BOX);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL1);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL2);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL3);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL4);
					m_bMsgBoxVisible = false;
				}
					break;

				case Action.ActionType.ACTION_PREVIOUS_MENU:
				{
					Log.Write("fullscreentv:goto previous menu");
					GUIWindowManager.ShowPreviousWindow();
					return;
				}

				case Action.ActionType.ACTION_REWIND:
				{
					if (g_Player.IsTimeShifting)
					{
						g_Player.Speed=Utils.GetNextRewindSpeed(g_Player.Speed);
						if (g_Player.Paused) g_Player.Pause();
						m_bUpdate=true;
					}
				}
					break;

				case Action.ActionType.ACTION_FORWARD:
				{
					if (g_Player.IsTimeShifting)
					{
						g_Player.Speed=Utils.GetNextForwardSpeed(g_Player.Speed);
						if (g_Player.Paused) g_Player.Pause();
						m_bUpdate=true;
					}
				}
					break;

				case Action.ActionType.ACTION_SHOW_GUI:
					Log.Write("fullscreentv:show gui");
					GUIWindowManager.ShowPreviousWindow();
					return;

				case Action.ActionType.ACTION_SHOW_OSD:	// Show the OSD
				{	
					Log.Write("OSD:ON");
					m_dwOSDTimeOut=DateTime.Now;
					GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_INIT,m_osdWindow.GetID,0,0,0,0,null);
					m_osdWindow.OnMessage(msg);	// Send an init msg to the OSD
					m_bOSDVisible=true;
					m_bUpdate=true;

				}
					break;

				case Action.ActionType.ACTION_STEP_BACK:
				{
					if (g_Player.IsTimeShifting)
					{
						m_bShowStep=true;
						m_dwTimeStatusShowTime=DateTime.Now;
						g_Player.SeekStep(false);
					}
				}
					break;

				case Action.ActionType.ACTION_STEP_FORWARD:
				{    
					if (g_Player.IsTimeShifting)
					{
						m_bShowStep=true;
						m_dwTimeStatusShowTime=DateTime.Now;
						g_Player.SeekStep(true);
					}
				}
					break;

				case Action.ActionType.ACTION_BIG_STEP_BACK:
				{
					if (g_Player.IsTimeShifting)
					{
						m_bShowInfo=true;
						m_dwTimeStatusShowTime=DateTime.Now;
						g_Player.SeekRelativePercentage(-10);
					}
				}
					break;

				case Action.ActionType.ACTION_BIG_STEP_FORWARD:
				{
					if (g_Player.IsTimeShifting)
					{
						m_bShowInfo=true;
						m_dwTimeStatusShowTime=DateTime.Now;
						g_Player.SeekRelativePercentage(10);
					}
				}
					break;
          
				case Action.ActionType.ACTION_PAUSE:
				{
					if (g_Player.IsTimeShifting) g_Player.Pause();
				}
					break;

				case Action.ActionType.ACTION_PLAY:
				case Action.ActionType.ACTION_MUSIC_PLAY:
					if (g_Player.IsTimeShifting)
					{
						g_Player.StepNow();
						g_Player.Speed=1;
						if (g_Player.Paused) g_Player.Pause();
					}
					break;

				case Action.ActionType.ACTION_CONTEXT_MENU:
					ShowContextMenu();
					break;
			}

			base.OnAction(action);
		}

		public override bool OnMessage(GUIMessage message)
		{
			m_bUpdate=true;
			if (m_bOSDVisible)
			{ 
				switch (message.Message)
				{
					case GUIMessage.MessageType.GUI_MSG_SETFOCUS:
						goto case GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT;
          
					case GUIMessage.MessageType.GUI_MSG_LOSTFOCUS:
						goto case GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT;

					case GUIMessage.MessageType.GUI_MSG_CLICKED:
						goto case GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT;

					case GUIMessage.MessageType.GUI_MSG_WINDOW_INIT:
						goto case GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT;

					case GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT:
						m_dwOSDTimeOut=DateTime.Now;
						break;
				}
				return m_osdWindow.OnMessage(message);	// route messages to OSD window
        
			}

			switch ( message.Message )
			{
				case GUIMessage.MessageType.GUI_MSG_HIDE_MESSAGE:
				{
					HideControl(GetID, (int)Control.MSG_BOX);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL1);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL2);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL3);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL4);
					m_bMsgBoxVisible = false;
				}
					break;

				case GUIMessage.MessageType.GUI_MSG_SHOW_MESSAGE:
				{
					// Todo : Overlay mode
					if (GUIGraphicsContext.Vmr9Active)
					{
						GUIMessage msg=new GUIMessage (GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID,0, (int)Control.MSG_BOX_LABEL1,0,0,null); 
						msg.Label=message.Label;
						OnMessage(msg);

						msg=new GUIMessage (GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID,0, (int)Control.MSG_BOX_LABEL2,0,0,null); 
						msg.Label=message.Label2;
						OnMessage(msg);

						msg=new GUIMessage (GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID,0, (int)Control.MSG_BOX_LABEL3,0,0,null); 
						msg.Label=message.Label3;
						OnMessage(msg);

						msg=new GUIMessage (GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID,0, (int)Control.MSG_BOX_LABEL4,0,0,null); 
						msg.Label=message.Label4;
						OnMessage(msg);

						if (message.Param2!=0) ShowControl(GetID, (int)Control.MSG_BOX);
						ShowControl(GetID, (int)Control.MSG_BOX_LABEL1);
						ShowControl(GetID, (int)Control.MSG_BOX_LABEL2);
						ShowControl(GetID, (int)Control.MSG_BOX_LABEL3);
						ShowControl(GetID, (int)Control.MSG_BOX_LABEL4);
						m_bMsgBoxVisible = true;
						
						// Set specified timeout
						m_iMsgBoxTimeout = message.Param1;
						m_dwMsgTimer = DateTime.Now;
					}
				}
					break;

				case GUIMessage.MessageType.GUI_MSG_MSN_CLOSECONVERSATION:
					if (m_bMSNChatVisible)
					{
						GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_msnWindow.GetID,0,0,0,0,null);
						m_msnWindow.OnMessage(msg);	// Send a de-init msg to the OSD
					}
					m_bMSNChatVisible=false;
					break;

				case GUIMessage.MessageType.GUI_MSG_MSN_STATUS_MESSAGE:
				case GUIMessage.MessageType.GUI_MSG_MSN_MESSAGE:
					if (m_bOSDVisible && m_bMSNChatPopup)
					{
						GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_osdWindow.GetID,0,0,0,0,null);
						m_osdWindow.OnMessage(msg);	// Send a de-init msg to the OSD
						m_bOSDVisible=false;
						m_bUpdate=true;
					}

					if (!m_bMSNChatVisible && m_bMSNChatPopup)
					{
						Log.Write("MSN CHAT:ON");     
						m_bMSNChatVisible=true;
						m_msnWindow.DoModal( GetID, message );
						m_bMSNChatVisible=false;
						m_bUpdate=true;         
					}
					break;
        
				case GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT:
				{
					Log.Write("deinit->OSD:Off");
					if (m_bOSDVisible)
					{
						GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_osdWindow.GetID,0,0,0,0,null);
						m_osdWindow.OnMessage(msg);	// Send a de-init msg to the OSD
					}
					m_bOSDVisible=false;

					if (m_bMSNChatVisible)
					{
						GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_msnWindow.GetID,0,0,0,0,null);
						m_msnWindow.OnMessage(msg);	// Send a de-init msg to the OSD
					}
					m_bMSNChatVisible=false;

					if (m_form!=null) 
					{
						m_form.Close();
						m_form.Dispose();
					}
					m_form=null;

					base.OnMessage(message);
					GUIGraphicsContext.IsFullScreenVideo=false;
					if ( !GUITVHome.IsTVWindow(message.Param1) )
					{
						if (! g_Player.Playing)
						{
							if (GUIGraphicsContext.ShowBackground)
							{
								// stop timeshifting & viewing... 
	              
								Recorder.StopViewing();
							}
						}
					}

					return true;
				}

				case GUIMessage.MessageType.GUI_MSG_WINDOW_INIT:
				{
					base.OnMessage(message);
					LoadSettings();
					GUIGraphicsContext.IsFullScreenVideo=true;
					m_channels.Clear();
					TVDatabase.GetChannels(ref m_channels);
					GUIGraphicsContext.VideoWindow = new Rectangle(GUIGraphicsContext.OverScanLeft, GUIGraphicsContext.OverScanTop, GUIGraphicsContext.OverScanWidth, GUIGraphicsContext.OverScanHeight);
					m_osdWindow=(GUITVOSD)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_TVOSD);
					m_zapWindow=(GUITVZAPOSD)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_TVZAPOSD);
					m_msnWindow=(GUITVMSNOSD)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_TVMSNOSD);

					HideControl(GetID, (int)Control.MSG_BOX);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL1);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL2);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL3);
					HideControl(GetID, (int)Control.MSG_BOX_LABEL4);
					m_bMsgBoxVisible = false;

					HideControl(GetID,(int)Control.LABEL_ROW1);
					HideControl(GetID,(int)Control.LABEL_ROW2);
					HideControl(GetID,(int)Control.LABEL_ROW3);
					HideControl(GetID,(int)Control.BLUE_BAR);
					HideControl(GetID,(int)Control.LABEL_CURRENT_TIME);
					HideControl(GetID,(int)Control.REC_LOGO);
					m_bLastPause=g_Player.Paused;
					m_iLastSpeed=g_Player.Speed;
					m_bClear=false;
					Log.Write("start fullscreen channel:{0}", Recorder.TVChannelName);
					Log.Write("init->OSD:Off");
					m_bOSDVisible=false;
					m_bShowInput=false;
					m_timeKeyPressed=DateTime.Now;
					m_strChannel="";
//					m_sZapChannel="";

					m_bLastStatusOSD=false;
					m_bOSDVisible=false;
					m_bUpdate=false;
					m_bLastStatus=false;
					m_UpdateTimer=DateTime.Now;
//					m_dwZapTimer=DateTime.Now;
					m_bClear=false;
					m_bShowInfo=false;
					m_bShowStep=false;
					m_bShowStatus=false;
					m_bShowGroup=false;
					m_dwTimeStatusShowTime=DateTime.Now;
					if (!GUIGraphicsContext.Vmr9Active)
					{
						m_form = new FormOSD();
						m_form.Owner = GUIGraphicsContext.form;
						m_form.Show();
						GUIGraphicsContext.form.Focus();
					}
                            
					GUIGraphicsContext.DX9Device.Clear( ClearFlags.Target, Color.Black, 1.0f, 0);
					try
					{
						GUIGraphicsContext.DX9Device.Present();
					}
					catch(Exception)
					{
					}
					return true;
				}
				case GUIMessage.MessageType.GUI_MSG_SETFOCUS:
					goto case GUIMessage.MessageType.GUI_MSG_LOSTFOCUS;

				case GUIMessage.MessageType.GUI_MSG_LOSTFOCUS:
					if (m_bOSDVisible) return true;
					if (m_bMSNChatVisible) return true;
					if (message.SenderControlId != (int)GUIWindow.Window.WINDOW_TVFULLSCREEN) return true;
					break;

			}

			if (m_bMSNChatVisible)
			{
				m_msnWindow.OnMessage(message);	// route messages to MSNChat window
			}

			return base.OnMessage(message);;
		}

		void ShowContextMenu()
		{
			if (dlg==null)
				dlg=(GUIDialogMenu)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
			if (dlg==null) return;
			dlg.Reset();
			dlg.SetHeading(924); // menu

			dlg.AddLocalizedString(915); // TV Channels
			if (GUITVHome.Navigator.Groups.Count > 1)
				dlg.AddLocalizedString(971); // Group
			if (Recorder.HasTeletext())
				dlg.AddLocalizedString(1441); // Fullscreen teletext
			dlg.AddLocalizedString(941); // Change aspect ratio
			if (PluginManager.IsPluginNameEnabled("MSN Messenger"))
			{

				dlg.AddLocalizedString(12902); // MSN Messenger
				dlg.AddLocalizedString(902); // MSN Online contacts
			}

			ArrayList	audioPidList = Recorder.GetAudioLanguageList();
			if (audioPidList!=null && audioPidList.Count>0)
			{
				dlg.AddLocalizedString(492); // Audio language menu
			}
			dlg.AddLocalizedString(970); // Previous window

			m_bDialogVisible=true;
			m_bUpdate=true;
			dlg.DoModal( GetID);
			m_bDialogVisible=false;
			m_bUpdate=true;
			if (dlg.SelectedId==-1) return;
			switch (dlg.SelectedId)
			{
				case 915: //TVChannels
				{
					dlg.Reset();
					dlg.SetHeading(GUILocalizeStrings.Get(915));//TV Channels
					foreach (TVChannel channel in GUITVHome.Navigator.CurrentGroup.tvChannels)
					{
						GUIListItem pItem = new GUIListItem(channel.Name);
						string strLogo=Utils.GetCoverArt(Thumbs.TVChannel,channel.Name);                   
						if (System.IO.File.Exists(strLogo))
						{										
							pItem.IconImage = strLogo;							
						}						
						dlg.Add(pItem);						
					}

					m_bDialogVisible=true;
					m_bUpdate=true;
					dlg.DoModal( GetID);
					m_bDialogVisible=false;
					m_bUpdate=true;

					if (dlg.SelectedLabel==-1) return;
					int tvChannelIndex=dlg.SelectedLabel;
					if (tvChannelIndex>=0 && tvChannelIndex < GUITVHome.Navigator.CurrentGroup.tvChannels.Count)
					{
						TVChannel channel = (TVChannel )GUITVHome.Navigator.CurrentGroup.tvChannels[tvChannelIndex];
						Log.Write("tv fs choose chan:{0}",channel.Name);
						GUITVHome.ViewChannel(channel.Name);
					}
				}
				break;

				case 971: //group
				{
					dlg.Reset();
					dlg.SetHeading(GUILocalizeStrings.Get(971));//Group
					foreach (TVGroup group in GUITVHome.Navigator.Groups)
					{
						dlg.Add(group.GroupName);
					}

					m_bDialogVisible=true;
					m_bUpdate=true;
					dlg.DoModal( GetID);
					m_bDialogVisible=false;
					m_bUpdate=true;

					if (dlg.SelectedLabel==-1) return;
					int selectedItem=dlg.SelectedLabel;
					if (selectedItem>=0 && selectedItem < GUITVHome.Navigator.Groups.Count)
					{
						TVGroup group = (TVGroup )GUITVHome.Navigator.Groups[selectedItem];
						GUITVHome.Navigator.SetCurrentGroup(group.GroupName);
					}
				}
					break;

				case 941: // Change aspect ratio
					ShowAspectRatioMenu();
					break;

				case 492: // Show audio language menu
					ShowAudioLanguageMenu();
					break;
	
				case 12902: // MSN Messenger
					Log.Write("MSN CHAT:ON");     
					m_bMSNChatVisible=true;
					m_msnWindow.DoModal( GetID, null );
					m_bMSNChatVisible=false;
					break;

				case 902: // Online contacts
					GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_MSN);
					break;

				case 1441: // Fullscreen teletext
					GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_FULLSCREEN_TELETEXT);
					break;

				case 970:
					// switch back to previous window
					m_bOSDVisible=false;
					m_bMSNChatVisible=false;
					GUIGraphicsContext.IsFullScreenVideo=false;
					GUIWindowManager.ShowPreviousWindow();
					break;
			}
		}
    
		void ShowAspectRatioMenu()
		{
			if (dlg==null) return;
			dlg.Reset();
			dlg.SetHeading(941); // Change aspect ratio

			dlg.AddLocalizedString(942); // Stretch
			dlg.AddLocalizedString(943); // Normal
			dlg.AddLocalizedString(944); // Original
			dlg.AddLocalizedString(945); // Letterbox
			dlg.AddLocalizedString(946); // Pan and scan
			dlg.AddLocalizedString(947); // Zoom

			m_bDialogVisible=true;
			m_bUpdate=true;
			dlg.DoModal( GetID);
			m_bDialogVisible=false;
			m_bUpdate=true;
			if (dlg.SelectedId==-1) return;
			switch (dlg.SelectedId)
			{
				case 942: // Stretch
					GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Stretch;
					m_bUpdate=true;
					SaveSettings();
					break;

				case 943: // Normal
					GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Normal;
					m_bUpdate=true;
					SaveSettings();
					break;

				case 944: // Original
					GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Original;
					m_bUpdate=true;
					SaveSettings();
					break;

				case 945: // Letterbox
					GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.LetterBox43;
					m_bUpdate=true;
					SaveSettings();
					break;

				case 946: // Pan and scan
					GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.PanScan43;
					m_bUpdate=true;
					SaveSettings();
					break;
			    
				case 947: // Zoom
					GUIGraphicsContext.ARType=MediaPortal.GUI.Library.Geometry.Type.Zoom;
					m_bUpdate=true;
					SaveSettings();
					break;
			}
		}
    
		void ShowAudioLanguageMenu()
		{
			if (dlg==null) return;
			dlg.Reset();			
			dlg.SetHeading(492); // set audio language menu

			DVBSections.AudioLanguage al;
			ArrayList	audioPidList = new ArrayList();
			audioPidList = Recorder.GetAudioLanguageList();

			DVBSections sections = new DVBSections();
			for (int i=0 ; i<audioPidList.Count ; i++)
			{				
				al = (DVBSections.AudioLanguage)audioPidList[i];				
				string strLanguage = sections.GetLanguageFromCode(al.AudioLanguageCode);
				dlg.Add(strLanguage);
			}

			m_bDialogVisible=true;
			m_bUpdate=true;
			dlg.DoModal( GetID);
			m_bDialogVisible=false;
			m_bUpdate=true;
			if (dlg.SelectedId==-1) return;

			// Set new language			
			if ( (dlg.SelectedId > 0) && (dlg.SelectedId <= audioPidList.Count) )
			{
				al = (DVBSections.AudioLanguage)audioPidList[dlg.SelectedId-1];
				Recorder.SetAudioLanguage(al.AudioPid);
			}

			// TODO : SaveSettings();
		}

		public bool NeedUpdate()
		{
			OnKeyTimeout();
			if (m_iLastSpeed != g_Player.Speed)
			{
				m_iLastSpeed=g_Player.Speed;
				if (m_iLastSpeed==1) m_bClear=true;

				if (m_bOSDVisible && (m_iLastSpeed==1))
				{        
					//Send play action to reset pressed buttons
					Action action=new Action();
					action.wID = Action.ActionType.ACTION_PLAY;
					m_osdWindow.OnAction(action);	// Route action to OSD window
				}
				m_bUpdate=true;
			}
			if (m_bLastPause!=g_Player.Paused)
			{
				m_bLastPause=g_Player.Paused;
				if (!m_bLastPause) m_bClear=true;
			}
			if (g_Player.Speed!=1 || g_Player.Paused)
			{
				m_bUpdate=true;
			}
			if (m_bLastStatus && !m_bOSDVisible)
			{
				m_bUpdate=true;
			}
			if (m_bShowInput)
			{
				m_bUpdate=true;
			}
			if (m_bShowStep || m_bShowStatus || m_bShowInfo)
			{
				TimeSpan ts=( DateTime.Now - m_dwTimeStatusShowTime);
				if ( ts.TotalSeconds>=5)
				{
					m_bClear=true;
					m_bShowInfo=false;
					m_bShowStep=false;
					m_bShowStatus=false;
					m_bUpdate=true;
				}
			}
			m_bLastStatus=m_bOSDVisible;
			if (m_bOSDVisible)
			{
				if (m_iMaxTimeOSDOnscreen>0)
				{
					TimeSpan ts =DateTime.Now - m_dwOSDTimeOut;
					if ( ts.TotalMilliseconds > m_iMaxTimeOSDOnscreen)
					{
						m_bUpdate=true;
					}
				}
				TimeSpan tsUpDate = DateTime.Now-m_UpdateTimer;
				if (tsUpDate.TotalSeconds>=1)
				{
					m_UpdateTimer=DateTime.Now;
					m_bUpdate=true;
				}
			}

			if (m_bZapOSDVisible && m_iZapTimeOut>0)
			{
				TimeSpan ts =DateTime.Now - m_dwZapTimer;
				if ( ts.TotalMilliseconds > m_iZapTimeOut)
				{
					//yes, then remove osd offscreen
					GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_zapWindow.GetID,0,0,0,0,null);
					m_zapWindow.OnMessage(msg);	// Send a de-init msg to the OSD
					Log.Write("timeout->ZAP OSD:Off");
					m_bZapOSDVisible=false;
					m_bUpdate=true;
				}
			}      

			if (m_bMSNChatVisible)
			{
				if (m_msnWindow.NeedRefresh()) m_bUpdate=true;
			}

			if (m_bDialogVisible)
			{
				if (dlg.NeedRefresh()) m_bUpdate=true;
			}

			if ( m_bUpdate)
			{
				m_bUpdate=false;
				return true;
			}
			return false;
		}

		public override void Process()
		{
			base.Process ();
			OnKeyTimeout();
			

			// Let the navigator zap channel if needed
			if ( GUITVHome.Navigator.CheckChannelChange())
			{
				Log.Write("zap osd off");
				if (m_bZapOSDVisible)
				{
					GUIMessage msg= new GUIMessage (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_zapWindow.GetID,0,0,0,0,null);
					m_zapWindow.OnMessage(msg);
					m_bZapOSDVisible=false;
				}
				m_bUpdate=true;
			}
			GUIGraphicsContext.IsFullScreenVideo=true;

			if (GUIGraphicsContext.Vmr9Active)
			{
				if (m_bShowInfo || m_bShowStep || m_bShowStatus)
				{
					TimeSpan ts = (DateTime.Now - m_dwTimeStatusShowTime);
					if (ts.TotalSeconds >= 5)
					{
						m_bShowInfo = false;
						m_bShowStep = false;
						m_bShowStatus = false;
					}
				}
				if (m_bShowGroup)
				{
					TimeSpan ts = (DateTime.Now - m_dwGroupZapTimer);
					if (ts.TotalMilliseconds >= m_iZapTimeOut)
					{
						Log.Write("Clear group");
						m_bShowGroup = false;
					}
				}

				SetFFRWLogos();
				ShowStatus();
			}
		}

		public override void Render(float timePassed)
		{
			if (GUIGraphicsContext.Vmr9Active)
			{
				base.Render(timePassed);

				// Message box still visible?
				if (m_bMsgBoxVisible && m_iMsgBoxTimeout>0)
				{
					TimeSpan ts = DateTime.Now - m_dwMsgTimer;
					if ( ts.TotalSeconds > m_iMsgBoxTimeout)
					{
						HideControl(GetID, (int)Control.MSG_BOX);
						HideControl(GetID, (int)Control.MSG_BOX_LABEL1);
						HideControl(GetID, (int)Control.MSG_BOX_LABEL2);
						HideControl(GetID, (int)Control.MSG_BOX_LABEL3);
						HideControl(GetID, (int)Control.MSG_BOX_LABEL4);
						m_bMsgBoxVisible = false;
					}
				}

				// do we need 2 render the OSD?
				if (m_bOSDVisible)
				{
					//yes
					m_osdWindow.Render(timePassed);          
        
					//times up?
					if (m_iMaxTimeOSDOnscreen>0)
					{
						TimeSpan ts =DateTime.Now - m_dwOSDTimeOut;
						if ( ts.TotalMilliseconds > m_iMaxTimeOSDOnscreen)
						{
							//yes, then remove osd offscreen
							GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_osdWindow.GetID,0,0,0,0,null);
							m_osdWindow.OnMessage(msg);	// Send a de-init msg to the OSD
							Log.Write("timeout->OSD:Off");
							m_bOSDVisible=false;
						}
					}
				}
				if (m_bZapOSDVisible)
				{
					m_zapWindow.Render(timePassed);

					if (m_iZapTimeOut>0)
					{
						TimeSpan ts =DateTime.Now - m_dwZapTimer;
						if ( ts.TotalMilliseconds > m_iZapTimeOut)
						{
							//yes, then remove osd offscreen
							GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_zapWindow.GetID,0,0,0,0,null);
							m_zapWindow.OnMessage(msg);	// Send a de-init msg to the OSD
							Log.Write("timeout->ZAP OSD:Off");
							m_bZapOSDVisible=false;
						}
					}
				}
			}

			if (Recorder.IsViewing()) return;
			if (g_Player.Playing && g_Player.IsTVRecording) return;

			//close window
			GUIMessage msg2= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_osdWindow.GetID,0,0,0,0,null);
			m_osdWindow.OnMessage(msg2);	// Send a de-init msg to the OSD
			Log.Write("timeout->OSD:Off");
			m_bOSDVisible=false;

			//close window
			msg2= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_msnWindow.GetID,0,0,0,0,null);
			m_msnWindow.OnMessage(msg2);	// Send a de-init msg to the OSD
			m_bMSNChatVisible=false;

			Log.Write("fullscreentv:not viewing anymore");
			GUIWindowManager.ShowPreviousWindow();
		}

		public void ShowStatus()
		{
			if (m_bShowInput)
			{
				ShowControl(GetID, (int)Control.BLUE_BAR);
				ShowControl(GetID, (int)Control.LABEL_ROW1);
				GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID,0, (int)Control.LABEL_ROW1,0,0,null); 
				msg.Label=String.Format("{0}:{1}", GUILocalizeStrings.Get(602),m_strChannel); 
				OnMessage(msg);
			}
			else if (m_bShowGroup)
			{
				ShowControl(GetID, (int)Control.BLUE_BAR);
				ShowControl(GetID, (int)Control.LABEL_ROW1);
				GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID, 0, (int)Control.LABEL_ROW1, 0, 0, null); 
				msg.Label=String.Format("{0}:{1}", GUILocalizeStrings.Get(971), GUITVHome.Navigator.ZapGroupName); 
				OnMessage(msg);
			}
			else if (m_bShowStep)
			{
				ShowControl(GetID, (int)Control.BLUE_BAR);
				ShowControl(GetID, (int)Control.LABEL_ROW1);
				GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID,0, (int)Control.LABEL_ROW1,0,0,null); 
				msg.Label=g_Player.GetStepDescription();
				OnMessage(msg);
			}
			else if (m_bShowStatus)
			{
				string strStatus="";
				switch (GUIGraphicsContext.ARType)
				{
					case MediaPortal.GUI.Library.Geometry.Type.Zoom:
					strStatus="Zoom";
					break;

					case MediaPortal.GUI.Library.Geometry.Type.Stretch:
					strStatus="Stretch";
					break;

					case MediaPortal.GUI.Library.Geometry.Type.Normal:
					strStatus="Normal";
					break;

					case MediaPortal.GUI.Library.Geometry.Type.Original:
					strStatus="Original";
					break;

					case MediaPortal.GUI.Library.Geometry.Type.LetterBox43:
					strStatus="Letterbox 4:3";
					break;

					case MediaPortal.GUI.Library.Geometry.Type.PanScan43:
					strStatus="Pan&Scan 4:3";
					break;
				}

				string strRects=String.Format(" | ({0},{1})-({2},{3})  ({4},{5})-({6},{7})", 
				g_Player.SourceWindow.Left,g_Player.SourceWindow.Top,
				g_Player.SourceWindow.Right,g_Player.SourceWindow.Bottom, 
				g_Player.VideoWindow.Left,g_Player.VideoWindow.Top,
				g_Player.VideoWindow.Right,g_Player.VideoWindow.Bottom);

				ShowControl(GetID, (int)Control.BLUE_BAR);
				ShowControl(GetID, (int)Control.LABEL_ROW1);
				GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_LABEL_SET, GetID,0, (int)Control.LABEL_ROW1,0,0,null); 
				msg.Label=strStatus;
				OnMessage(msg);
			}
			else
			{
				HideControl(GetID, (int)Control.BLUE_BAR);
				HideControl(GetID, (int)Control.LABEL_ROW1);        
			}
		}

		public void UpdateOSD()
		{
			if (m_bOSDVisible)
			{
				m_osdWindow.UpdateChannelInfo();
				m_dwOSDTimeOut=DateTime.Now;
				m_dwZapTimer=DateTime.Now;
				m_bUpdate = true;
			}
			else
			{
				m_zapWindow.UpdateChannelInfo();
				Action myaction=new Action();
				myaction.wID = Action.ActionType.ACTION_SHOW_INFO;
				OnAction(myaction);
				m_dwZapTimer=DateTime.Now;
			} 
		}

		public void SetFFRWLogos()
		{
			//if (GUIGraphicsContext.Vmr9Active && (m_bShowStatus||m_bShowInfo || m_bShowStep || (!m_bOSDVisible&& g_Player.Speed!=1) || (!m_bOSDVisible&& g_Player.Paused)) )
			if ((m_bShowStatus || m_bShowInfo || m_bShowStep || (!m_bOSDVisible && g_Player.Speed!=1) || (!m_bOSDVisible&& g_Player.Paused)) )
			{
				if (!m_bOSDVisible)
				{
					for (int i=(int)Control.OSD_VIDEOPROGRESS; i < (int)Control.OSD_VIDEOPROGRESS+20;++i)
						ShowControl(GetID,i);

					// Set recorder status
					if (Recorder.IsRecordingChannel(GUITVHome.Navigator.CurrentChannel))
					{
						ShowControl(GetID, (int)Control.REC_LOGO);
					}
				}
				else
				{
					for (int i=(int)Control.OSD_VIDEOPROGRESS; i < (int)Control.OSD_VIDEOPROGRESS+20;++i)
						HideControl(GetID,i);
					HideControl(GetID, (int)Control.REC_LOGO);
				}
			}
			else
			{
				for (int i=(int)Control.OSD_VIDEOPROGRESS; i < (int)Control.OSD_VIDEOPROGRESS+20;++i)
					HideControl(GetID,i);
				HideControl(GetID, (int)Control.REC_LOGO);
			}

			if (g_Player.Paused )
			{
				ShowControl(GetID,(int)Control.IMG_PAUSE);  
			}
			else
			{
				HideControl(GetID,(int)Control.IMG_PAUSE);  
			}

			int iSpeed=g_Player.Speed;
			HideControl(GetID,(int)Control.IMG_2X);
			HideControl(GetID,(int)Control.IMG_4X);
			HideControl(GetID,(int)Control.IMG_8X);
			HideControl(GetID,(int)Control.IMG_16X);
			HideControl(GetID,(int)Control.IMG_32X);
			HideControl(GetID,(int)Control.IMG_MIN2X);
			HideControl(GetID,(int)Control.IMG_MIN4X);
			HideControl(GetID,(int)Control.IMG_MIN8X);
			HideControl(GetID,(int)Control.IMG_MIN16X);
			HideControl(GetID,(int)Control.IMG_MIN32X);

			if(iSpeed!=1)
			{
				if(iSpeed == 2)
				{
					ShowControl(GetID,(int)Control.IMG_2X);
				}
				else if(iSpeed == 4)
				{
					ShowControl(GetID,(int)Control.IMG_4X);
				}
				else if(iSpeed == 8)
				{
					ShowControl(GetID,(int)Control.IMG_8X);
				}
				else if(iSpeed == 16)
				{
					ShowControl(GetID,(int)Control.IMG_16X);
				}
				else if(iSpeed == 32)
				{
					ShowControl(GetID,(int)Control.IMG_32X);
				}

				if(iSpeed == -2)
				{
					ShowControl(GetID,(int)Control.IMG_MIN2X);
				}
				else if(iSpeed == -4)
				{
					ShowControl(GetID,(int)Control.IMG_MIN4X);
				}
				else if(iSpeed == -8)
				{
					ShowControl(GetID,(int)Control.IMG_MIN8X);
				}
				else if(iSpeed == -16)
				{
					ShowControl(GetID,(int)Control.IMG_MIN16X);
				}
				else if(iSpeed == -32)
				{
					ShowControl(GetID,(int)Control.IMG_MIN32X);
				}
			}
		}

		public void RenderForm(float timePassed)
		{
			bool bClear=false;

			if (m_bDialogVisible)
			{
				if (!m_bLastDialogVisible)
				{
					m_bLastDialogVisible=true;
					bClear=true;			
				}
			}
			else
			{
				if (m_bLastDialogVisible)
				{
					m_bLastDialogVisible=false;
					bClear=true;
				}
			}

			if (m_bLastMSNChatVisible)
			{
				if (!m_bMSNChatVisible)
				{
					bClear=true;			
					m_bLastMSNChatVisible=false;
				}
			}

			if (m_bZapOSDVisible)
			{
				m_bLastZapOSDVisible = true;
			}

			if (m_bLastZapOSDVisible)
			{
				if (!m_bZapOSDVisible)
				{
				bClear=true;			
				m_bLastZapOSDVisible=false;
				}
			}

			if (m_bZapOSDVisible)
			{
				m_bLastZapOSDVisible = true;
			}

			// if last time OSD was visible
			if (m_bLastStatusOSD)
			{
				// and now its gone
				if (!m_bOSDVisible)
				{
					// then clear screen
					bClear=true;
				}
				else 
				{
					// osd still onscreen, check if it needs a refresh
					if (m_osdWindow.NeedRefresh() || m_zapWindow.NeedRefresh())
					{
						// yes, then clear screen
						bClear=true;
					}
				}
			}

      
			// clear screen...
			if (bClear||m_bClear)
			{
				GUIGraphicsContext.graphics.Clear(Color.Black);
				Trace.WriteLine("osd:Clear window");
			}
			m_bClear=false;

      
			SetFFRWLogos();
			ShowStatus();
			base.Render(timePassed);
			if (GUIGraphicsContext.graphics!=null)
			{
				if (m_bDialogVisible)
				{
					dlg.Render(timePassed);
				}

				if (m_bMSNChatVisible)
				{
					m_msnWindow.Render(timePassed);
				}
			}
			// do we need 2 render the OSD?
			if (m_bOSDVisible)
			{
				//yes
				m_bLastStatusOSD=true;
				m_osdWindow.Render(timePassed);
        
				//times up?
				if (m_iMaxTimeOSDOnscreen>0)
				{
					TimeSpan ts =DateTime.Now - m_dwOSDTimeOut;
					if ( ts.TotalMilliseconds > m_iMaxTimeOSDOnscreen)
					{
						//yes, then remove osd offscreen
						GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_osdWindow.GetID,0,0,0,0,null);
						m_osdWindow.OnMessage(msg);	// Send a de-init msg to the OSD

						m_bOSDVisible=false;
					}
				}
			}
			else  if (m_bZapOSDVisible)
			{
				m_zapWindow.Render(timePassed);

				if (m_iZapTimeOut>0)
				{
					TimeSpan ts =DateTime.Now - m_dwZapTimer;
					if ( ts.TotalMilliseconds > m_iZapTimeOut)
					{
						//yes, then remove osd offscreen
						GUIMessage msg= new GUIMessage  (GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT,m_zapWindow.GetID,0,0,0,0,null);
						m_zapWindow.OnMessage(msg);	// Send a de-init msg to the OSD
						Log.Write("timeout->ZAP OSD:Off");
						m_bZapOSDVisible=false;
					}
				}
			}
			else
			{
				m_bLastStatusOSD=false;
			}
		}
        
		void HideControl (int dwSenderId, int dwControlID) 
		{
			GUIControl cntl=base.GetControl(dwControlID);
			if (cntl!=null)
			{
				cntl.IsVisible=false;
			}
		}
		void ShowControl (int dwSenderId, int dwControlID) 
		{
			GUIControl cntl=base.GetControl(dwControlID);
			if (cntl!=null)
			{
				cntl.IsVisible=true;
			}
		}

		void OnKeyTimeout()
		{
			if (m_strChannel.Length==0) return;
			TimeSpan ts=DateTime.Now-m_timeKeyPressed;
			if (ts.TotalMilliseconds>=1000)
			{
				// change channel
				int iChannel=Int32.Parse(m_strChannel);
				ChangeChannelNr(iChannel);
				m_bShowInput=false;
				m_bClear=true;
				m_strChannel="";
			}
		}
		private void OnKeyCode(char chKey)
		{
			if (chKey >= '0' && chKey <= '9') //Make sure it's only for the remote
			{
				m_bShowInput = true;
				m_timeKeyPressed = DateTime.Now;
				m_strChannel += chKey;
				if (m_strChannel.Length == 3)
				{
					// Change channel immediately
					int iChannel = Int32.Parse(m_strChannel);
					ChangeChannelNr(iChannel);
					m_bShowInput = false;
					m_strChannel = "";
				}
			}
		}

		private void OnPageDown()
		{
			// Switch to the next channel group and tune to the first channel in the group
			GUITVHome.Navigator.ZapToPreviousGroup(true);
			m_bShowGroup = true;
			m_dwGroupZapTimer = DateTime.Now;
		}

		private void OnPageUp()
		{
			// Switch to the next channel group and tune to the first channel in the group
			GUITVHome.Navigator.ZapToNextGroup(true);
			m_bShowGroup = true;
			m_dwGroupZapTimer = DateTime.Now;
		}

		void ChangeChannelNr(int channelNr)
		{
			GUITVHome.Navigator.ZapToChannel(channelNr, false);
			UpdateOSD();
			m_dwZapTimer=DateTime.Now;
			m_bClear = true;				// Clear screen during next render
		}

		public void ZapPreviousChannel()
		{
			GUITVHome.Navigator.ZapToPreviousChannel(true);
			UpdateOSD();
			m_dwZapTimer = DateTime.Now;
		}

		public void ZapNextChannel()
		{
			GUITVHome.Navigator.ZapToNextChannel(true);
			UpdateOSD();
			m_dwZapTimer = DateTime.Now;
		}


		public override int GetFocusControlId()
		{
			if (m_bOSDVisible) 
			{
				return m_osdWindow.GetFocusControlId();
			}
			if (m_bMSNChatVisible)
			{
				return m_msnWindow.GetFocusControlId();
			}

			return base.GetFocusControlId();
		}

		public override GUIControl	GetControl(int iControlId) 
		{
			if (m_bOSDVisible) 
			{
				return m_osdWindow.GetControl(iControlId);
			}
			if (m_bMSNChatVisible)
			{
				return m_msnWindow.GetControl(iControlId);
			}

			return base.GetControl(iControlId);
		}
	}
}
