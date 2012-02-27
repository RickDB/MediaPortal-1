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
using System.Collections.Generic;
using System.Windows.Forms;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.SetupControls.UserInterfaceControls;
using Mediaportal.TV.Server.SetupTV.Dialogs;
using Mediaportal.TV.Server.SetupTV.PlaylistSupport;
using Mediaportal.TV.Server.SetupTV.Sections.Helpers;
using Mediaportal.TV.Server.TVControl;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Factories;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVLibrary.Interfaces;

using System.Threading;
using Mediaportal.TV.Server.TVService.Interfaces;
using Mediaportal.TV.Server.TVService.Interfaces.Enums;
using Mediaportal.TV.Server.TVService.Interfaces.Services;
using Mediaportal.TV.Server.TVService.ServiceAgents;

namespace Mediaportal.TV.Server.SetupTV.Sections
{
  public partial class RadioChannels : SectionSettings
  {
    public class CardInfo
    {
      protected Card _card;

      public Card Card
      {
        get { return _card; }
      }

      public CardInfo(Card card)
      {
        _card = card;
      }

      public override string ToString()
      {
        return _card.name;
      }
    }

    private readonly MPListViewStringColumnSorter lvwColumnSorter;
    private readonly MPListViewStringColumnSorter lvwColumnSorter2;
    private ChannelListViewHandler _lvChannelHandler;

    private bool _suppressRefresh = false;
    private bool _isScanning = false;
    private bool _abortScanning = false;
    private Thread _scanThread;

    private Dictionary<int, CardType> _cards = null;
    private IList<Channel> _allChannels = null;

    public RadioChannels()
      : this("Radio Channels")
    {
      mpListView1.IsChannelListView = true;
      tabControl1.AllowReorderTabs = true;
    }

    public RadioChannels(string name)
      : base(name)
    {
      InitializeComponent();

      lvwColumnSorter = new MPListViewStringColumnSorter();
      lvwColumnSorter.Order = SortOrder.None;
      lvwColumnSorter2 = new MPListViewStringColumnSorter();
      lvwColumnSorter2.Order = SortOrder.Descending;
      lvwColumnSorter2.OrderType = MPListViewStringColumnSorter.OrderTypes.AsValue;
      mpListView1.ListViewItemSorter = lvwColumnSorter;
    }

    public override void OnSectionDeActivated()
    {
      ServiceAgents.Instance.ControllerServiceAgent.OnNewSchedule();
      base.OnSectionDeActivated();
    }


    public override void OnSectionActivated()
    {
      base.OnSectionActivated();

      this.RefreshAll();
    }

    private void RefreshAll()
    {
      this.RefreshTabs();
      this.RefreshContextMenu();

      Application.DoEvents();

      this.RefreshAllChannels();
    }

    private void RefreshTabs()
    {
      // bugfix for tab removal, RemoveAt fails sometimes
      tabControl1.TabPages.Clear();
      tabControl1.TabPages.Add(tabPage1);

      IList<ChannelGroup> groups =
        ServiceAgents.Instance.ChannelGroupServiceAgent.ListAllChannelGroupsByMediaType(MediaTypeEnum.Radio);

      foreach (ChannelGroup group in groups)
      {
        TabPage page = new TabPage(group.groupName);
        page.SuspendLayout();

        ChannelsInRadioGroupControl channelsInRadioGroupControl = new ChannelsInRadioGroupControl();
        channelsInRadioGroupControl.Location = new System.Drawing.Point(9, 9);
        channelsInRadioGroupControl.Anchor = ((AnchorStyles.Top | AnchorStyles.Bottom)
                                              | AnchorStyles.Left)
                                             | AnchorStyles.Right;

        page.Controls.Add(channelsInRadioGroupControl);

        page.Tag = group;
        page.Location = new System.Drawing.Point(4, 22);
        page.Padding = new Padding(3);
        page.Size = new System.Drawing.Size(457, 374);
        page.UseVisualStyleBackColor = true;
        page.PerformLayout();
        page.ResumeLayout(false);

        tabControl1.TabPages.Add(page);
      }
    }

    private void RefreshContextMenu()
    {
      addToFavoritesToolStripMenuItem.DropDownItems.Clear();

      IList<ChannelGroup> groups = ServiceAgents.Instance.ChannelGroupServiceAgent.ListAllChannelGroupsByMediaType(MediaTypeEnum.Radio);

      foreach (ChannelGroup group in groups)
      {
        ToolStripMenuItem item = new ToolStripMenuItem(group.groupName);

        item.Tag = group;
        item.Click += OnAddToFavoritesMenuItem_Click;

        addToFavoritesToolStripMenuItem.DropDownItems.Add(item);
      }

      ToolStripMenuItem itemNew = new ToolStripMenuItem("New...");
      itemNew.Click += OnAddToFavoritesMenuItem_Click;
      addToFavoritesToolStripMenuItem.DropDownItems.Add(itemNew);
    }

    private void RefreshAllChannels()
    {
      Cursor.Current = Cursors.WaitCursor;
      IList<Card> dbsCards = ServiceAgents.Instance.CardServiceAgent.ListAllCards(CardIncludeRelationEnum.None);
      _cards = new Dictionary<int, CardType>();
      foreach (Card card in dbsCards)
      {
        _cards[card.idCard] = ServiceAgents.Instance.ControllerServiceAgent.Type(card.idCard);
      }

      _allChannels = ServiceAgents.Instance.ChannelServiceAgent.ListAllChannelsByMediaType(MediaTypeEnum.Radio);

      tabControl1.TabPages[0].Text = string.Format("Channels ({0})", _allChannels.Count);

      _lvChannelHandler = new ChannelListViewHandler(mpListView1, _allChannels, _cards, txtFilterString,
                                                     MediaTypeEnum.Radio);
      _lvChannelHandler.FilterListView("");
    }

    private void txtFilterString_TextChanged(object sender, EventArgs e)
    {
      //Filter the listview so only items that contain the text of txtFilterString are shown
      _lvChannelHandler.FilterListView(txtFilterString.Text);
    }

    private void OnAddToFavoritesMenuItem_Click(object sender, EventArgs e)
    {
      ChannelGroup group;
      ToolStripMenuItem menuItem = (ToolStripMenuItem)sender;
      if (menuItem.Tag == null)
      {
        GroupNameForm dlg = new GroupNameForm();
        dlg.MediaType = MediaTypeEnum.Radio;
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
          return;
        }
        group = new ChannelGroup {groupName = dlg.GroupName, sortOrder = 999};
        group = ServiceAgents.Instance.ChannelGroupServiceAgent.SaveGroup(group);
        group.AcceptChanges();

        this.RefreshContextMenu();
        this.RefreshTabs();
      }
      else
      {
        group = (ChannelGroup)menuItem.Tag;
      }

      ListView.SelectedIndexCollection indexes = mpListView1.SelectedIndices;
      if (indexes.Count == 0)
        return;
      for (int i = 0; i < indexes.Count; ++i)
      {
        ListViewItem item = mpListView1.Items[indexes[i]];

        Channel channel = (Channel)item.Tag;        
        MappingHelper.AddChannelToGroup(ref channel, group, MediaTypeEnum.Radio);                

        string groupString = item.SubItems[1].Text;
        if (groupString == string.Empty)
        {
          groupString = group.groupName;
        }
        else
        {
          groupString += ", " + group.groupName;
        }

        item.SubItems[1].Text = groupString;
      }

      mpListView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
    }

    private void mpButtonClear_Click(object sender, EventArgs e)
    {
      string holder = String.Format("Are you sure you want to clear all radio channels?");

      if (MessageBox.Show(holder, "", MessageBoxButtons.YesNo) == DialogResult.No)
      {
        return;
      }

      NotifyForm dlg = new NotifyForm("Clearing all radio channels...",
                                      "This can take some time\n\nPlease be patient...");
      dlg.Show(this);
      dlg.WaitForDisplay();
      IList<Channel> channels = ServiceAgents.Instance.ChannelServiceAgent.ListAllChannels();
      foreach (Channel channel in channels)
      {
        if (channel.mediaType == (int)MediaTypeEnum.Radio)
        {
          //channel.TvMovieMappings = null;

          //Broker.Execute("delete from TvMovieMapping WHERE idChannel=" + channel.idChannel);
          ServiceAgents.Instance.ChannelServiceAgent.DeleteChannel(channel.idChannel);
        }
      }
      dlg.Close();
    
      OnSectionActivated();
    }

    private void mpButtonDel_Click(object sender, EventArgs e)
    {
      mpListView1.BeginUpdate();
      try
      {
        if (mpListView1.SelectedItems.Count > 0)
        {
          string holder = String.Format("Are you sure you want to delete these {0:d} radio channels?",
                                        mpListView1.SelectedItems.Count);

          if (MessageBox.Show(holder, "", MessageBoxButtons.YesNo) == DialogResult.No)
          {
            //mpListView1.EndUpdate();
            return;
          }
        }
        NotifyForm dlg = new NotifyForm("Deleting selected radio channels...",
                                        "This can take some time\n\nPlease be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();

        foreach (ListViewItem item in mpListView1.SelectedItems)
        {
          Channel channel = (Channel)item.Tag;
          IList<GroupMap> mapsRadio = channel.GroupMaps;
          // Bav: fixing Mantis bug 1178: Can't delete Radio channels in SetupTV
          foreach (GroupMap map in mapsRadio)
          {
            ServiceAgents.Instance.ChannelGroupServiceAgent.DeleteChannelGroupMap(map.idMap)          ;
          }
          IList<GroupMap> maps = channel.GroupMaps;
          foreach (GroupMap map in maps)
          {
            ServiceAgents.Instance.ChannelGroupServiceAgent.DeleteChannelGroupMap(map.idMap)          ;
          }
          // Bav - End of fix
          ServiceAgents.Instance.ChannelServiceAgent.DeleteChannel(channel.idChannel);
          mpListView1.Items.Remove(item);
        }
        dlg.Close();
        ReOrder();
      }
      finally
      {
        mpListView1.EndUpdate();
      }
    }

    private void ReOrder()
    {
      IList<Channel> channels = new List<Channel>();
      for (int i = 0; i < mpListView1.Items.Count; ++i)
      {
        Channel channel = (Channel)mpListView1.Items[i].Tag;
        if (channel.sortOrder != i)
        {
          channel.sortOrder = i;
          channels.Add(channel);
          channel.AcceptChanges();
        }
      }
      ServiceAgents.Instance.ChannelServiceAgent.SaveChannels(channels);
    }

    private void ReOrderGroups()
    {
      for (int i = 1; i < tabControl1.TabPages.Count; i++)
      {
        ChannelGroup group = (ChannelGroup)tabControl1.TabPages[i].Tag;
        group.sortOrder = i - 1;
        group = ServiceAgents.Instance.ChannelGroupServiceAgent.SaveGroup(group);
        group.AcceptChanges();
      }
      RefreshAll();
    }

    private void mpListView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
    {
      if (e.Label != null)
      {
        Channel channel = (Channel)mpListView1.Items[e.Item].Tag;
        channel.displayName = e.Label;
        ServiceAgents.Instance.ChannelServiceAgent.SaveChannel(channel);
      }
    }

    private void mpListView1_ItemChecked(object sender, ItemCheckedEventArgs e)
    {
      Channel ch = (Channel)e.Item.Tag;
      if (ch.visibleInGuide != e.Item.Checked && !_lvChannelHandler.PopulateRunning)
      {
        ch.visibleInGuide = e.Item.Checked;
        ServiceAgents.Instance.ChannelServiceAgent.SaveChannel(ch);
      }
    }

    private void mpButtonEdit_Click(object sender, EventArgs e)
    {
      ListView.SelectedIndexCollection indexes = mpListView1.SelectedIndices;
      if (indexes.Count == 0)
        return;
      Channel channel = (Channel)mpListView1.Items[indexes[0]].Tag;
      FormEditChannel dlg = new FormEditChannel();
      dlg.Channel = channel;      
      dlg.MediaType = MediaTypeEnum.Radio;
      if (dlg.ShowDialog(this) == DialogResult.OK)
      {
        IList<Card> dbsCards = ServiceAgents.Instance.CardServiceAgent.ListAllCards(CardIncludeRelationEnum.None);
        Dictionary<int, CardType> cards = new Dictionary<int, CardType>();
        foreach (Card card in dbsCards)
        {
          cards[card.idCard] = ServiceAgents.Instance.ControllerServiceAgent.Type(card.idCard);
        }
        mpListView1.BeginUpdate();
        try
        {
          mpListView1.Items[indexes[0]] = _lvChannelHandler.CreateListViewItemForChannel(channel, cards);
          mpListView1.Sort();
          ReOrder();
        }
        finally
        {
          mpListView1.EndUpdate();
        }
      }
    }

    private void mpListView1_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == lvwColumnSorter.SortColumn)
      {
        // Reverse the current sort direction for this column.
        lvwColumnSorter.Order = lvwColumnSorter.Order == SortOrder.Ascending
                                  ? SortOrder.Descending
                                  : SortOrder.Ascending;
      }
      else
      {
        // Set the column number that is to be sorted; default to ascending.
        lvwColumnSorter.SortColumn = e.Column;
        lvwColumnSorter.Order = SortOrder.Ascending;
      }

      // Perform the sort with these new sort options.
      mpListView1.Sort();
      ReOrder();
    }

    private void mpListView1_ItemDrag(object sender, ItemDragEventArgs e)
    {
      if (e.Item is ListViewItem)
      {
        ReOrder();
      }
    }

    private void mpListView1_MouseDoubleClick(object sender, MouseEventArgs e)
    {
      mpButtonEdit_Click(null, null);
    }

    private void deleteThisChannelToolStripMenuItem_Click(object sender, EventArgs e)
    {
      mpButtonDel_Click(null, null);
    }

    private void editChannelToolStripMenuItem_Click(object sender, EventArgs e)
    {
      mpButtonEdit_Click(null, null);
    }

    private void mpButtonAdd_Click(object sender, EventArgs e)
    {
      FormEditChannel dlg = new FormEditChannel();
      dlg.Channel = null;      
      dlg.MediaType = MediaTypeEnum.Radio;
      if (dlg.ShowDialog(this) == DialogResult.OK)
      {
        IList<Card> dbsCards = ServiceAgents.Instance.CardServiceAgent.ListAllCards(CardIncludeRelationEnum.None);
        Dictionary<int, CardType> cards = new Dictionary<int, CardType>();
        foreach (Card card in dbsCards)
        {
          cards[card.idCard] = ServiceAgents.Instance.ControllerServiceAgent.Type(card.idCard);
        }
        mpListView1.BeginUpdate();
        try
        {
          mpListView1.Items.Add(_lvChannelHandler.CreateListViewItemForChannel(dlg.Channel, cards));
          mpListView1.Sort();
          ReOrder();
        }
        finally
        {
          mpListView1.EndUpdate();
        }
      }
    }

    private void mpButtonUncheckEncrypted_Click(object sender, EventArgs e)
    {
      NotifyForm dlg = new NotifyForm("Unchecking all scrambled tv channels...",
                                      "This can take some time\n\nPlease be patient...");
      dlg.Show(this);
      dlg.WaitForDisplay();
      foreach (ListViewItem item in mpListView1.Items)
      {
        Channel channel = (Channel)item.Tag;
        bool hasFTA = false;
        foreach (TuningDetail tuningDetail in channel.TuningDetails)
        {
          if (tuningDetail.freeToAir)
          {
            hasFTA = true;
            break;
          }
        }
        if (!hasFTA)
        {
          item.Checked = false;
        }
      }
      dlg.Close();
    }

    private void mpButtonDeleteEncrypted_Click(object sender, EventArgs e)
    {
      NotifyForm dlg = new NotifyForm("Deleting all scrambled tv channels...",
                                      "This can take some time\n\nPlease be patient...");
      dlg.Show(this);
      dlg.WaitForDisplay();
      List<ListViewItem> itemsToRemove = new List<ListViewItem>();
      foreach (ListViewItem item in mpListView1.Items)
      {
        Channel channel = (Channel)item.Tag;
        bool hasFTA = false;
        foreach (TuningDetail tuningDetail in channel.TuningDetails)
        {
          if (tuningDetail.freeToAir)
          {
            hasFTA = true;
            break;
          }
        }
        if (!hasFTA)
        {
          ServiceAgents.Instance.ChannelServiceAgent.DeleteChannel(channel.idChannel);
          itemsToRemove.Add(item);
        }
      }
      foreach (ListViewItem item in itemsToRemove)
        mpListView1.Items.Remove(item);
      dlg.Close();
      ReOrder();
      ServiceAgents.Instance.ControllerServiceAgent.OnNewSchedule();
      mpListView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
    }

    private void btnPlaylist_Click(object sender, EventArgs e)
    {
      OpenFileDialog dlg = new OpenFileDialog();
      dlg.AddExtension = false;
      dlg.CheckFileExists = true;
      dlg.CheckPathExists = true;
      dlg.Filter = "playlists (*.m3u;*.pls;*.b4s;*.wpl)|*.m3u;*.pls;*.b4s;*.wpl";
      dlg.Multiselect = false;
      dlg.Title = "Select the playlist file to import";
      if (dlg.ShowDialog(this) != DialogResult.OK)
        return;
      IPlayListIO listIO = PlayListFactory.CreateIO(dlg.FileName);
      PlayList playlist = new PlayList();
      if (!listIO.Load(playlist, dlg.FileName))
      {
        MessageBox.Show("There was an error parsing the playlist file", "Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
        return;
      }
      
      int iInserted = 0;
      foreach (PlayListItem item in playlist)
      {
        if (string.IsNullOrEmpty(item.FileName))
          continue;
        if (string.IsNullOrEmpty(item.Description))
          item.Description = item.FileName;
        Channel channel = ChannelFactory.CreateChannel(MediaTypeEnum.Radio, 0, Schedule.MinSchedule, false,
                                      Schedule.MinSchedule, 10000, true, "", item.Description);
        ServiceAgents.Instance.ChannelServiceAgent.SaveChannel(channel);

        TuningDetail detail = new TuningDetail
                                {
                                  mediaType = (int) MediaTypeEnum.Radio,
                                  url = item.FileName,
                                  name = channel.displayName,
                                  idChannel = channel.idChannel
                                };
        ServiceAgents.Instance.ChannelServiceAgent.SaveTuningDetail(detail);
        MappingHelper.AddChannelToGroup(ref channel, TvConstants.RadioGroupNames.AllChannels, MediaTypeEnum.Radio);                
        iInserted++;
      }
      MessageBox.Show("Imported " + iInserted + " new channels from playlist");
      OnSectionActivated();
    }

    private void mpButtonPreview_Click(object sender, EventArgs e)
    {
      ListView.SelectedIndexCollection indexes = mpListView1.SelectedIndices;
      if (indexes.Count == 0)
        return;
      Channel channel = (Channel)mpListView1.Items[indexes[0]].Tag;
      FormPreview previewWindow = new FormPreview();
      previewWindow.Channel = channel;
      previewWindow.ShowDialog(this);
    }

    private void renameSelectedChannelsBySIDToolStripMenuItem_Click(object sender, EventArgs e)
    {
      NotifyForm dlg = new NotifyForm("Renaming selected tv channels by SID ...",
                                      "This can take some time\n\nPlease be patient...");
      dlg.Show(this);
      dlg.WaitForDisplay();
      foreach (ListViewItem item in mpListView1.SelectedItems)
      {
        Channel channel = (Channel)item.Tag;
        IList<TuningDetail> details = channel.TuningDetails;
        if (details.Count > 0)
        {
          channel.displayName = (details[0]).serviceId.ToString();
          ServiceAgents.Instance.ChannelServiceAgent.SaveChannel(channel);
          item.Tag = channel;
        }
      }
      dlg.Close();
      OnSectionActivated();
    }

    private void addSIDInFrontOfNameToolStripMenuItem_Click(object sender, EventArgs e)
    {
      NotifyForm dlg = new NotifyForm("Adding SID in front of name...",
                                      "This can take some time\n\nPlease be patient...");
      dlg.Show(this);
      dlg.WaitForDisplay();

      foreach (ListViewItem item in mpListView1.SelectedItems)
      {
        Channel channel = (Channel)item.Tag;
        IList<TuningDetail> details = channel.TuningDetails;
        if (details.Count > 0)
        {
          channel.displayName = (details[0]).serviceId + " " + channel.displayName;
          ServiceAgents.Instance.ChannelServiceAgent.SaveChannel(channel);
          item.Tag = channel;
        }
      }
      dlg.Close();
      OnSectionActivated();
    }

    private void renumberChannelsBySIDToolStripMenuItem_Click(object sender, EventArgs e)
    {
      NotifyForm dlg = new NotifyForm("Renumbering radio channels...", "This can take some time\n\nPlease be patient...");
      dlg.Show(this);
      dlg.WaitForDisplay();

      foreach (ListViewItem item in mpListView1.SelectedItems)
      {
        Channel channel = (Channel)item.Tag;
        IList<TuningDetail> details = channel.TuningDetails;
        foreach (TuningDetail detail in details)
        {
          detail.channelNumber = detail.serviceId;
          ServiceAgents.Instance.ChannelServiceAgent.SaveTuningDetail(detail);
        }
      }
      dlg.Close();
    }

    private void StartScanThread()
    {
      _scanThread = new Thread(ScanForUsableChannels);
      _scanThread.Name = "Channels test thread";
      _scanThread.Start();
      mpButtonTestScrambled.Text = "Stop";
    }

    private void StopScanThread()
    {
      _abortScanning = true;
    }

    private void mpButtonTestScrambled_Click(object sender, EventArgs e)
    {
      if (_isScanning)
      {
        StopScanThread();
      }
      else if (!_abortScanning) // cancel in progress
      {
        StartScanThread();
      }
    }

    private void ScanForUsableChannels()
    {
      _abortScanning = false;
      _isScanning = true;
      NotifyForm dlg = new NotifyForm("Testing all checked radio channels...", "Please be patient...");
      dlg.Show(this);
      dlg.WaitForDisplay();

      // Create tunning objects Server, User and Card
      IUser _user = new User();
      IVirtualCard _card;

      foreach (ListViewItem item in mpListView1.Items)
      {
        if (item.Checked == false)
        {
          continue; // do not test "un-checked" channels
        }
        Channel _channel = (Channel)item.Tag; // get channel
        dlg.SetMessage(
          string.Format("Please be patient...\n\nTesting channel {0} ( {1} of {2} )",
                        _channel.displayName, item.Index + 1, mpListView1.Items.Count));
        Application.DoEvents();
        TvResult result = ServiceAgents.Instance.ControllerServiceAgent.StartTimeShifting(ref _user, _channel.idChannel, out _card);
        if (result == TvResult.Succeeded)
        {
          _card.StopTimeShifting();
        }
        else
        {
          item.Checked = false;
          _channel.visibleInGuide = false;
          ServiceAgents.Instance.ChannelServiceAgent.SaveChannel(_channel);
        }
        if (_abortScanning)
        {
          break;
        }
      }
      mpButtonTestScrambled.Text = "Test";
      dlg.Close();
      _isScanning = false;
      _abortScanning = false;
    }

    private void mpButtonUp_Click(object sender, EventArgs e)
    {
      mpListView1.BeginUpdate();
      try
      {
        ListView.SelectedIndexCollection indexes = mpListView1.SelectedIndices;
        if (indexes.Count == 0)
          return;
        for (int i = 0; i < indexes.Count; ++i)
        {
          int index = indexes[i];
          if (index > 0)
          {
            ListViewItem item = mpListView1.Items[index];
            mpListView1.Items.RemoveAt(index);
            mpListView1.Items.Insert(index - 1, item);
          }
        }
        ReOrder();
      }
      finally
      {
        mpListView1.EndUpdate();
      }
    }

    private void mpButtonDown_Click(object sender, EventArgs e)
    {
      mpListView1.BeginUpdate();
      try
      {
        ListView.SelectedIndexCollection indexes = mpListView1.SelectedIndices;
        if (indexes.Count == 0)
          return;
        if (mpListView1.Items.Count < 2)
          return;
        for (int i = indexes.Count - 1; i >= 0; i--)
        {
          int index = indexes[i];
          ListViewItem item = mpListView1.Items[index];
          mpListView1.Items.RemoveAt(index);
          if (index + 1 < mpListView1.Items.Count)
            mpListView1.Items.Insert(index + 1, item);
          else
            mpListView1.Items.Add(item);
        }
        ReOrder();
      }
      finally
      {
        mpListView1.EndUpdate();
      }
    }

    private void mpButtonAddGroup_Click(object sender, EventArgs e)
    {
      GroupNameForm dlg = new GroupNameForm();
      dlg.MediaType = MediaTypeEnum.Radio;

      if (dlg.ShowDialog(this) != DialogResult.OK)
      {
        return;
      }
      
      ServiceAgents.Instance.ChannelGroupServiceAgent.GetOrCreateGroup(dlg.GroupName);

      this.RefreshContextMenu();
      this.RefreshTabs();
    }

    private void mpButtonRenameGroup_Click(object sender, EventArgs e)
    {
      GroupSelectionForm dlgGrpSel = new GroupSelectionForm();
      dlgGrpSel.Selection = GroupSelectionForm.SelectionType.ForRenaming;

      if (dlgGrpSel.ShowDialog(typeof (ChannelGroup), this) != DialogResult.OK)
      {
        return;
      }

      ChannelGroup group = dlgGrpSel.Group as ChannelGroup;
      if (group == null)
      {
        return;
      }

      GroupNameForm dlgGrpName = new GroupNameForm(group.groupName);
      if (dlgGrpName.ShowDialog(this) != DialogResult.OK)
      {
        return;
      }

      group.groupName = dlgGrpName.GroupName;
      group = ServiceAgents.Instance.ChannelGroupServiceAgent.SaveGroup(group);
      group.AcceptChanges();

      if (group.GroupMaps.Count > 0)
      {
        this.RefreshAll();
      }
      else
      {
        this.RefreshContextMenu();
        this.RefreshTabs();
      }
    }

    private void mpButtonDelGroup_Click(object sender, EventArgs e)
    {
      GroupSelectionForm dlgGrpSel = new GroupSelectionForm();

      if (dlgGrpSel.ShowDialog(typeof (ChannelGroup), this) != DialogResult.OK)
      {
        return;
      }

      ChannelGroup group = dlgGrpSel.Group as ChannelGroup;
      if (group == null)
      {
        return;
      }

      DialogResult result = MessageBox.Show(string.Format("Are you sure you want to delete the group '{0}'?",
                                                          group.groupName), "", MessageBoxButtons.YesNo);

      if (result == DialogResult.No)
      {
        return;
      }

      bool isGroupEmpty = (group.GroupMaps.Count <= 0);
      ServiceAgents.Instance.ChannelGroupServiceAgent.DeleteChannelGroup(group.idGroup);      

      if (!isGroupEmpty)
      {
        this.RefreshAll();
      }
      else
      {
        this.RefreshContextMenu();
        this.RefreshTabs();
      }
    }

    private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (_suppressRefresh)
      {
        return;
      }

      if (tabControl1.SelectedIndex == 0)
      {
        OnSectionActivated();
      }
      else
      {
        if (tabControl1.TabCount > 0)
        {
          TabPage page = tabControl1.TabPages[tabControl1.SelectedIndex];
          foreach (Control control in page.Controls)
          {
            ChannelsInRadioGroupControl groupCnt = control as ChannelsInRadioGroupControl;
            if (groupCnt != null)
            {
              groupCnt.Group = (ChannelGroup)page.Tag;
              groupCnt.OnActivated();
            }
          }
        }
      }
    }

    private void tabControl1_DragOver(object sender, DragEventArgs e)
    {
      //means a channel group assignment is going to be performed
      if (e.Data.GetData(typeof (MPListView)) != null)
      {
        for (int i = 0; i < tabControl1.TabPages.Count; i++)
        {
          if (i == tabControl1.SelectedIndex)
          {
            continue;
          }

          if (tabControl1.GetTabRect(i).Contains(this.PointToClient(new System.Drawing.Point(e.X, e.Y))))
          {
            tabControl1.SelectedIndex = i;
            break;
          }
        }
      }
    }

    private void tabControl1_DragDrop(object sender, DragEventArgs e)
    {
      TabPage droppedTabPage = e.Data.GetData(typeof (TabPage)) as TabPage;
      if (droppedTabPage == null)
      {
        return;
      }

      int targetIndex = -1;


      System.Drawing.Point pt = new System.Drawing.Point(e.X, e.Y);

      pt = PointToClient(pt);

      for (int i = 0; i < tabControl1.TabPages.Count; i++)
      {
        if (tabControl1.GetTabRect(i).Contains(pt))
        {
          targetIndex = i;
          break;
        }
      }

      if (targetIndex < 0)
      {
        return;
      }

      _suppressRefresh = true;

      int sourceIndex = tabControl1.TabPages.IndexOf(droppedTabPage);

      //it looks a bit ugly when the first tab gets the focus, due to the other design
      if (sourceIndex == tabControl1.TabPages.Count - 1)
      {
        tabControl1.SelectedIndex = sourceIndex - 1;
      }
      else
      {
        tabControl1.DeselectTab(sourceIndex);
      }

      tabControl1.TabPages.RemoveAt(sourceIndex);

      tabControl1.TabPages.Insert(targetIndex, droppedTabPage);
      tabControl1.SelectedIndex = targetIndex;

      _suppressRefresh = false;

      this.ReOrderGroups();
    }

    private void tabControl1_MouseClick(object sender, MouseEventArgs e)
    {
      if (e.Button != MouseButtons.Right)
      {
        return;
      }

      int targetIndex = -1;
      System.Drawing.Point pt = new System.Drawing.Point(e.X, e.Y);

      for (int i = 0; i < tabControl1.TabPages.Count; i++)
      {
        if (tabControl1.GetTabRect(i).Contains(pt))
        {
          targetIndex = i;
          break;
        }
      }

      //first tab isn't a group tab
      if (targetIndex < 1)
      {
        return;
      }

      ChannelGroup group = tabControl1.TabPages[targetIndex].Tag as ChannelGroup;
      if (group == null)
      {
        return;
      }

      bool isFixedGroupName = (
                                group.groupName == TvConstants.TvGroupNames.AllChannels ||
                                group.groupName == TvConstants.TvGroupNames.Analog ||
                                group.groupName == TvConstants.TvGroupNames.DVBC ||
                                group.groupName == TvConstants.TvGroupNames.DVBS ||
                                group.groupName == TvConstants.TvGroupNames.DVBT
                              );

      bool isGlobalChannelsGroup = (
                                     group.groupName == TvConstants.TvGroupNames.AllChannels
                                   );

      renameGroupToolStripMenuItem.Tag = tabControl1.TabPages[targetIndex];
      deleteGroupToolStripMenuItem.Tag = renameGroupToolStripMenuItem.Tag;

      renameGroupToolStripMenuItem.Enabled = !isFixedGroupName;
      deleteGroupToolStripMenuItem.Enabled = !isGlobalChannelsGroup;

      pt = tabControl1.PointToScreen(pt);

      groupTabContextMenuStrip.Show(pt);
    }

    private void renameGroupToolStripMenuItem_Click(object sender, EventArgs e)
    {
      ToolStripDropDownItem menuItem = sender as ToolStripDropDownItem;
      if (menuItem == null)
      {
        return;
      }

      TabPage tab = menuItem.Tag as TabPage;
      if (tab == null)
      {
        return;
      }

      ChannelGroup group = tab.Tag as ChannelGroup;
      if (group == null)
      {
        return;
      }

      GroupNameForm dlg = new GroupNameForm(group.groupName);

      dlg.ShowDialog(this);

      if (dlg.GroupName.Length == 0)
      {
        return;
      }

      group.groupName = dlg.GroupName;
      group = ServiceAgents.Instance.ChannelGroupServiceAgent.SaveGroup(group);
      group.AcceptChanges();

      tab.Text = dlg.GroupName;

      if (group.GroupMaps.Count > 0 && tabControl1.SelectedIndex == 0)
      {
        this.RefreshContextMenu();
        this.RefreshAllChannels();
      }
      else
      {
        this.RefreshContextMenu();
      }
    }

    private void deleteGroupToolStripMenuItem_Click(object sender, EventArgs e)
    {
      ToolStripDropDownItem menuItem = sender as ToolStripDropDownItem;
      if (menuItem == null)
      {
        return;
      }

      TabPage tab = menuItem.Tag as TabPage;
      if (tab == null)
      {
        return;
      }

      ChannelGroup group = tab.Tag as ChannelGroup;
      if (group == null)
      {
        return;
      }

      DialogResult result = MessageBox.Show(string.Format("Are you sure you want to delete the group '{0}'?",
                                                          group.groupName), "", MessageBoxButtons.YesNo);

      if (result == DialogResult.No)
      {
        return;
      }

      bool groupIsEmpty = (group.GroupMaps.Count <= 0);

      ServiceAgents.Instance.ChannelGroupServiceAgent.DeleteChannelGroup(group.idGroup);      
      tabControl1.TabPages.Remove(tab);

      if (!groupIsEmpty && tabControl1.SelectedIndex == 0)
      {
        this.RefreshContextMenu();
        this.RefreshAllChannels();
      }
      else
      {
        this.RefreshContextMenu();
      }
    }
  }
}