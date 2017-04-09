﻿// OpenTween - Client of Twitter
// Copyright (c) 2007-2011 kiri_feather (@kiri_feather) <kiri.feather@gmail.com>
//           (c) 2008-2011 Moz (@syo68k)
//           (c) 2008-2011 takeshik (@takeshik) <http://www.takeshik.org/>
//           (c) 2010-2011 anis774 (@anis774) <http://d.hatena.ne.jp/anis774/>
//           (c) 2010-2011 fantasticswallow (@f_swallow) <http://twitter.com/f_swallow>
//           (c) 2011      kim_upsilon (@kim_upsilon) <https://upsilo.net/~upsilon/>
// All rights reserved.
// 
// This file is part of OpenTween.
// 
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
// 
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details. 
// 
// You should have received a copy of the GNU General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Web;
using System.IO;
using System.Net;
using OpenTween.Api;
using OpenTween.Api.DataModel;
using OpenTween.Connection;
using System.Diagnostics.CodeAnalysis;

namespace OpenTween
{
    public partial class UserInfoDialog : OTBaseForm
    {
        private TwitterUser _displayUser;
        private CancellationTokenSource cancellationTokenSource = null;

        private readonly TweenMain mainForm;
        private readonly TwitterApi twitterApi;

        public UserInfoDialog(TweenMain mainForm, TwitterApi twitterApi)
        {
            this.mainForm = mainForm;
            this.twitterApi = twitterApi;

            InitializeComponent();

            // LabelScreenName のフォントを OTBaseForm.GlobalFont に変更
            this.LabelScreenName.Font = this.ReplaceToGlobalFont(this.LabelScreenName.Font);
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope")]
        private void CancelLoading()
        {
            CancellationTokenSource newTokenSource = null, oldTokenSource = null;
            try
            {
                newTokenSource = new CancellationTokenSource();
                oldTokenSource = Interlocked.Exchange(ref this.cancellationTokenSource, newTokenSource);
            }
            finally
            {
                if (oldTokenSource != null)
                {
                    oldTokenSource.Cancel();
                    oldTokenSource.Dispose();
                }
            }
        }

        public async Task ShowUserAsync(TwitterUser user)
        {
            if (this.IsDisposed)
                return;

            if (user == null || user == this._displayUser)
                return;

            this.CancelLoading();

            var cancellationToken = this.cancellationTokenSource.Token;

            this._displayUser = user;

            this.LabelId.Text = user.IdStr;
            this.LabelScreenName.Text = user.ScreenName;
            this.LabelName.Text = user.Name;
            this.LabelLocation.Text = user.Location ?? "";
            this.LabelCreatedAt.Text = MyCommon.DateTimeParse(user.CreatedAt).ToString();

            if (user.Protected)
                this.LabelIsProtected.Text = Properties.Resources.Yes;
            else
                this.LabelIsProtected.Text = Properties.Resources.No;

            if (user.Verified)
                this.LabelIsVerified.Text = Properties.Resources.Yes;
            else
                this.LabelIsVerified.Text = Properties.Resources.No;

            var followingUrl = "https://twitter.com/" + user.ScreenName + "/following";
            this.LinkLabelFollowing.Text = user.FriendsCount.ToString();
            this.LinkLabelFollowing.Tag = followingUrl;
            this.ToolTip1.SetToolTip(this.LinkLabelFollowing, followingUrl);

            var followersUrl = "https://twitter.com/" + user.ScreenName + "/followers";
            this.LinkLabelFollowers.Text = user.FollowersCount.ToString();
            this.LinkLabelFollowers.Tag = followersUrl;
            this.ToolTip1.SetToolTip(this.LinkLabelFollowers, followersUrl);

            var favoritesUrl = "https://twitter.com/" + user.ScreenName + "/favorites";
            this.LinkLabelFav.Text = user.FavouritesCount.ToString();
            this.LinkLabelFav.Tag = favoritesUrl;
            this.ToolTip1.SetToolTip(this.LinkLabelFav, favoritesUrl);

            var profileUrl = "https://twitter.com/" + user.ScreenName;
            this.LinkLabelTweet.Text = user.StatusesCount.ToString();
            this.LinkLabelTweet.Tag = profileUrl;
            this.ToolTip1.SetToolTip(this.LinkLabelTweet, profileUrl);

            if (this.twitterApi.CurrentUserId == user.Id)
            {
                this.ButtonEdit.Enabled = true;
                this.ChangeIconToolStripMenuItem.Enabled = true;
                this.ButtonBlock.Enabled = false;
                this.ButtonReportSpam.Enabled = false;
                this.ButtonBlockDestroy.Enabled = false;
            }
            else
            {
                this.ButtonEdit.Enabled = false;
                this.ChangeIconToolStripMenuItem.Enabled = false;
                this.ButtonBlock.Enabled = true;
                this.ButtonReportSpam.Enabled = true;
                this.ButtonBlockDestroy.Enabled = true;
            }

            await Task.WhenAll(new[]
            {
                this.SetDescriptionAsync(user.Description, user.Entities.Description, cancellationToken),
                this.SetRecentStatusAsync(user.Status, cancellationToken),
                this.SetLinkLabelWebAsync(user.Url, user.Entities.Url, cancellationToken),
                this.SetUserImageAsync(user.ProfileImageUrlHttps, cancellationToken),
                this.LoadFriendshipAsync(user.ScreenName, cancellationToken),
            });
        }

        private async Task SetDescriptionAsync(string descriptionText, TwitterEntities entities, CancellationToken cancellationToken)
        {
            if (descriptionText != null)
            {
                foreach (var entity in entities.Urls)
                    entity.ExpandedUrl = await ShortUrl.Instance.ExpandUrlAsync(entity.ExpandedUrl);

                // user.entities には urls 以外のエンティティが含まれていないため、テキストをもとに生成する
                entities.Hashtags = TweetExtractor.ExtractHashtagEntities(descriptionText).ToArray();
                entities.UserMentions = TweetExtractor.ExtractMentionEntities(descriptionText).ToArray();

                var html = TweetFormatter.AutoLinkHtml(descriptionText, entities);
                html = this.mainForm.createDetailHtml(html);

                if (cancellationToken.IsCancellationRequested)
                    return;

                this.DescriptionBrowser.DocumentText = html;
            }
            else
            {
                this.DescriptionBrowser.DocumentText = "";
            }
        }

        private async Task SetUserImageAsync(string imageUri, CancellationToken cancellationToken)
        {
            var oldImage = this.UserPicture.Image;
            this.UserPicture.Image = null;
            oldImage?.Dispose();

            // ProfileImageUrlHttps が null になる場合があるらしい
            // 参照: https://sourceforge.jp/ticket/browse.php?group_id=6526&tid=33871
            if (imageUri == null)
                return;

            await this.UserPicture.SetImageFromTask(async () =>
            {
                var uri = imageUri.Replace("_normal", "_bigger");

                using (var imageStream = await Networking.Http.GetStreamAsync(uri).ConfigureAwait(false))
                {
                    var image = await MemoryImage.CopyFromStreamAsync(imageStream)
                        .ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        image.Dispose();
                        throw new OperationCanceledException(cancellationToken);
                    }

                    return image;
                }
            });
        }

        private async Task SetLinkLabelWebAsync(string uri, TwitterEntities entities, CancellationToken cancellationToken)
        {
            if (uri != null)
            {
                var origUrl = entities?.Urls[0].ExpandedUrl ?? uri;
                var expandedUrl = await ShortUrl.Instance.ExpandUrlAsync(origUrl);

                if (cancellationToken.IsCancellationRequested)
                    return;

                this.LinkLabelWeb.Text = expandedUrl;
                this.LinkLabelWeb.Tag = expandedUrl;
                this.ToolTip1.SetToolTip(this.LinkLabelWeb, expandedUrl);
            }
            else
            {
                this.LinkLabelWeb.Text = "";
                this.LinkLabelWeb.Tag = null;
                this.ToolTip1.SetToolTip(this.LinkLabelWeb, null);
            }
        }

        private async Task SetRecentStatusAsync(TwitterStatus status, CancellationToken cancellationToken)
        {
            if (status != null)
            {
                var entities = status.MergedEntities;

                foreach (var entity in entities.Urls)
                    entity.ExpandedUrl = await ShortUrl.Instance.ExpandUrlAsync(entity.ExpandedUrl);

                var html = TweetFormatter.AutoLinkHtml(status.FullText, entities);
                html = this.mainForm.createDetailHtml(html +
                    " Posted at " + MyCommon.DateTimeParse(status.CreatedAt) +
                    " via " + status.Source);

                if (cancellationToken.IsCancellationRequested)
                    return;

                this.RecentPostBrowser.DocumentText = html;
            }
            else
            {
                this.RecentPostBrowser.DocumentText = Properties.Resources.ShowUserInfo2;
            }
        }

        private async Task LoadFriendshipAsync(string screenName, CancellationToken cancellationToken)
        {
            this.LabelIsFollowing.Text = "";
            this.LabelIsFollowed.Text = "";
            this.ButtonFollow.Enabled = false;
            this.ButtonUnFollow.Enabled = false;

            if (this.twitterApi.CurrentScreenName == screenName)
                return;

            TwitterFriendship friendship;
            try
            {
                friendship = await this.twitterApi.FriendshipsShow(this.twitterApi.CurrentScreenName, screenName);
            }
            catch (WebApiException)
            {
                LabelIsFollowed.Text = Properties.Resources.GetFriendshipInfo6;
                LabelIsFollowing.Text = Properties.Resources.GetFriendshipInfo6;
                return;
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            var isFollowing = friendship.Relationship.Source.Following;
            var isFollowedBy = friendship.Relationship.Source.FollowedBy;

            this.LabelIsFollowing.Text = isFollowing
                ? Properties.Resources.GetFriendshipInfo1
                : Properties.Resources.GetFriendshipInfo2;

            this.LabelIsFollowed.Text = isFollowedBy
                ? Properties.Resources.GetFriendshipInfo3
                : Properties.Resources.GetFriendshipInfo4;

            this.ButtonFollow.Enabled = !isFollowing;
            this.ButtonUnFollow.Enabled = isFollowing;
        }

        private void ShowUserInfo_Load(object sender, EventArgs e)
        {
            this.TextBoxName.Location = this.LabelName.Location;
            this.TextBoxName.Height = this.LabelName.Height;
            this.TextBoxName.Width = this.LabelName.Width;
            this.TextBoxName.BackColor = this.mainForm.InputBackColor;
            this.TextBoxName.MaxLength = 20;

            this.TextBoxLocation.Location = this.LabelLocation.Location;
            this.TextBoxLocation.Height = this.LabelLocation.Height;
            this.TextBoxLocation.Width = this.LabelLocation.Width;
            this.TextBoxLocation.BackColor = this.mainForm.InputBackColor;
            this.TextBoxLocation.MaxLength = 30;

            this.TextBoxWeb.Location = this.LinkLabelWeb.Location;
            this.TextBoxWeb.Height = this.LinkLabelWeb.Height;
            this.TextBoxWeb.Width = this.LinkLabelWeb.Width;
            this.TextBoxWeb.BackColor = this.mainForm.InputBackColor;
            this.TextBoxWeb.MaxLength = 100;

            this.TextBoxDescription.Location = this.DescriptionBrowser.Location;
            this.TextBoxDescription.Height = this.DescriptionBrowser.Height;
            this.TextBoxDescription.Width = this.DescriptionBrowser.Width;
            this.TextBoxDescription.BackColor = this.mainForm.InputBackColor;
            this.TextBoxDescription.MaxLength = 160;
            this.TextBoxDescription.Multiline = true;
            this.TextBoxDescription.ScrollBars = ScrollBars.Vertical;
        }

        private async void LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var linkLabel = (LinkLabel)sender;

            var linkUrl = (string)linkLabel.Tag;
            if (linkUrl == null)
                return;

            await this.mainForm.OpenUriInBrowserAsync(linkUrl);
        }

        private async void ButtonFollow_Click(object sender, EventArgs e)
        {
            using (ControlTransaction.Disabled(this.ButtonFollow))
            {
                try
                {
                    await this.twitterApi.FriendshipsCreate(this._displayUser.ScreenName)
                        .IgnoreResponse();
                }
                catch (WebApiException ex)
                {
                    MessageBox.Show(Properties.Resources.FRMessage2 + ex.Message);
                    return;
                }
            }

            MessageBox.Show(Properties.Resources.FRMessage3);
            LabelIsFollowing.Text = Properties.Resources.GetFriendshipInfo1;
            ButtonFollow.Enabled = false;
            ButtonUnFollow.Enabled = true;
        }

        private async void ButtonUnFollow_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this._displayUser.ScreenName + Properties.Resources.ButtonUnFollow_ClickText1,
                               Properties.Resources.ButtonUnFollow_ClickText2,
                               MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                using (ControlTransaction.Disabled(this.ButtonUnFollow))
                {
                    try
                    {
                        await this.twitterApi.FriendshipsDestroy(this._displayUser.ScreenName)
                            .IgnoreResponse();
                    }
                    catch (WebApiException ex)
                    {
                        MessageBox.Show(Properties.Resources.FRMessage2 + ex.Message);
                        return;
                    }
                }

                MessageBox.Show(Properties.Resources.FRMessage3);
                LabelIsFollowing.Text = Properties.Resources.GetFriendshipInfo2;
                ButtonFollow.Enabled = true;
                ButtonUnFollow.Enabled = false;
            }
        }

        private void ShowUserInfo_Activated(object sender, EventArgs e)
        {
            //画面が他画面の裏に隠れると、アイコン画像が再描画されない問題の対応
            if (UserPicture.Image != null)
                UserPicture.Invalidate(false);
        }

        private void ShowUserInfo_Shown(object sender, EventArgs e)
        {
            ButtonClose.Focus();
        }

        private async void WebBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.AbsoluteUri != "about:blank")
            {
                e.Cancel = true;

                // Ctrlを押しながらリンクを開いた場合は、設定と逆の動作をするフラグを true としておく
                await this.mainForm.OpenUriAsync(e.Url, MyCommon.IsKeyDown(Keys.Control));
            }
        }

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var browser = (WebBrowser)this.ContextMenuRecentPostBrowser.SourceControl;
            browser.Document.ExecCommand("SelectAll", false, null);
        }

        private void SelectionCopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var browser = (WebBrowser)this.ContextMenuRecentPostBrowser.SourceControl;
            var selectedText = browser.GetSelectedText();
            if (selectedText != null)
            {
                try
                {
                    Clipboard.SetDataObject(selectedText, false, 5, 100);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void ContextMenuRecentPostBrowser_Opening(object sender, CancelEventArgs e)
        {
            var browser = (WebBrowser)this.ContextMenuRecentPostBrowser.SourceControl;
            var selectedText = browser.GetSelectedText();

            this.SelectionCopyToolStripMenuItem.Enabled = selectedText != null;
        }

        private async void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            await this.mainForm.OpenUriInBrowserAsync("https://support.twitter.com/groups/31-twitter-basics/topics/111-features/articles/268350-x8a8d-x8a3c-x6e08-x307f-x30a2-x30ab-x30a6-x30f3-x30c8-x306b-x3064-x3044-x3066");
        }

        private async void LinkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            await this.mainForm.OpenUriInBrowserAsync("https://support.twitter.com/groups/31-twitter-basics/topics/107-my-profile-account-settings/articles/243055-x516c-x958b-x3001-x975e-x516c-x958b-x30a2-x30ab-x30a6-x30f3-x30c8-x306b-x3064-x3044-x3066");
        }

        private void ButtonSearchPosts_Click(object sender, EventArgs e)
        {
            this.mainForm.AddNewTabForUserTimeline(this._displayUser.ScreenName);
        }

        private async void UserPicture_Click(object sender, EventArgs e)
        {
            var imageUrl = this._displayUser.ProfileImageUrlHttps;
            imageUrl = imageUrl.Remove(imageUrl.LastIndexOf("_normal", StringComparison.Ordinal), 7);

            await this.mainForm.OpenUriInBrowserAsync(imageUrl);
        }

        private bool IsEditing = false;
        private string ButtonEditText = "";

        private async void ButtonEdit_Click(object sender, EventArgs e)
        {
            // 自分以外のプロフィールは変更できない
            if (this.twitterApi.CurrentUserId != this._displayUser.Id)
                return;

            using (ControlTransaction.Disabled(this.ButtonEdit))
            {
                if (!IsEditing)
                {
                    ButtonEditText = ButtonEdit.Text;
                    ButtonEdit.Text = Properties.Resources.UserInfoButtonEdit_ClickText1;

                    TextBoxName.Text = LabelName.Text;
                    TextBoxName.Enabled = true;
                    TextBoxName.Visible = true;
                    LabelName.Visible = false;

                    TextBoxLocation.Text = LabelLocation.Text;
                    TextBoxLocation.Enabled = true;
                    TextBoxLocation.Visible = true;
                    LabelLocation.Visible = false;

                    TextBoxWeb.Text = this._displayUser.Url;
                    TextBoxWeb.Enabled = true;
                    TextBoxWeb.Visible = true;
                    LinkLabelWeb.Visible = false;

                    TextBoxDescription.Text = this._displayUser.Description;
                    TextBoxDescription.Enabled = true;
                    TextBoxDescription.Visible = true;
                    DescriptionBrowser.Visible = false;

                    TextBoxName.Focus();
                    TextBoxName.Select(TextBoxName.Text.Length, 0);

                    IsEditing = true;
                }
                else
                {
                    Task showUserTask = null;

                    if (TextBoxName.Modified ||
                        TextBoxLocation.Modified ||
                        TextBoxWeb.Modified ||
                        TextBoxDescription.Modified)
                    {
                        try
                        {
                            var response = await this.twitterApi.AccountUpdateProfile(
                                this.TextBoxName.Text,
                                this.TextBoxWeb.Text,
                                this.TextBoxLocation.Text,
                                this.TextBoxDescription.Text);

                            var user = await response.LoadJsonAsync();
                            showUserTask = this.ShowUserAsync(user);
                        }
                        catch (WebApiException ex)
                        {
                            MessageBox.Show($"Err:{ex.Message}(AccountUpdateProfile)");
                            return;
                        }
                    }

                    TextBoxName.Enabled = false;
                    TextBoxName.Visible = false;
                    LabelName.Visible = true;

                    TextBoxLocation.Enabled = false;
                    TextBoxLocation.Visible = false;
                    LabelLocation.Visible = true;

                    TextBoxWeb.Enabled = false;
                    TextBoxWeb.Visible = false;
                    LinkLabelWeb.Visible = true;

                    TextBoxDescription.Enabled = false;
                    TextBoxDescription.Visible = false;
                    DescriptionBrowser.Visible = true;

                    ButtonEdit.Text = ButtonEditText;

                    IsEditing = false;

                    if (showUserTask != null)
                        await showUserTask;
                }
            }
        }

        private async Task DoChangeIcon(string filename)
        {
            try
            {
                var mediaItem = new FileMediaItem(filename);

                await this.twitterApi.AccountUpdateProfileImage(mediaItem)
                    .IgnoreResponse();
            }
            catch (WebApiException ex)
            {
                MessageBox.Show("Err:" + ex.Message + Environment.NewLine + Properties.Resources.ChangeIconToolStripMenuItem_ClickText4);
                return;
            }

            MessageBox.Show(Properties.Resources.ChangeIconToolStripMenuItem_ClickText5);

            try
            {
                var user = await this.twitterApi.UsersShow(this._displayUser.ScreenName);

                if (user != null)
                    await this.ShowUserAsync(user);
            }
            catch (WebApiException ex)
            {
                MessageBox.Show($"Err:{ex.Message}(UsersShow)");
            }
        }

        private async void ChangeIconToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialogIcon.Filter = Properties.Resources.ChangeIconToolStripMenuItem_ClickText1;
            OpenFileDialogIcon.Title = Properties.Resources.ChangeIconToolStripMenuItem_ClickText2;
            OpenFileDialogIcon.FileName = "";

            DialogResult rslt = OpenFileDialogIcon.ShowDialog();

            if (rslt != DialogResult.OK)
            {
                return;
            }

            string fn = OpenFileDialogIcon.FileName;
            if (this.IsValidIconFile(new FileInfo(fn)))
            {
                await this.DoChangeIcon(fn);
            }
            else
            {
                MessageBox.Show(Properties.Resources.ChangeIconToolStripMenuItem_ClickText6);
            }
        }

        private async void ButtonBlock_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this._displayUser.ScreenName + Properties.Resources.ButtonBlock_ClickText1,
                                Properties.Resources.ButtonBlock_ClickText2,
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                using (ControlTransaction.Disabled(this.ButtonBlock))
                {
                    try
                    {
                        await this.twitterApi.BlocksCreate(this._displayUser.ScreenName)
                            .IgnoreResponse();
                    }
                    catch (WebApiException ex)
                    {
                        MessageBox.Show("Err:" + ex.Message + Environment.NewLine + Properties.Resources.ButtonBlock_ClickText3);
                        return;
                    }

                    MessageBox.Show(Properties.Resources.ButtonBlock_ClickText4);
                }
            }
        }

        private async void ButtonReportSpam_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this._displayUser.ScreenName + Properties.Resources.ButtonReportSpam_ClickText1,
                                Properties.Resources.ButtonReportSpam_ClickText2,
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                using (ControlTransaction.Disabled(this.ButtonReportSpam))
                {
                    try
                    {
                        await this.twitterApi.UsersReportSpam(this._displayUser.ScreenName)
                            .IgnoreResponse();
                    }
                    catch (WebApiException ex)
                    {
                        MessageBox.Show("Err:" + ex.Message + Environment.NewLine + Properties.Resources.ButtonReportSpam_ClickText3);
                        return;
                    }

                    MessageBox.Show(Properties.Resources.ButtonReportSpam_ClickText4);
                }
            }
        }

        private async void ButtonBlockDestroy_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this._displayUser.ScreenName + Properties.Resources.ButtonBlockDestroy_ClickText1,
                                Properties.Resources.ButtonBlockDestroy_ClickText2,
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                using (ControlTransaction.Disabled(this.ButtonBlockDestroy))
                {
                    try
                    {
                        await this.twitterApi.BlocksDestroy(this._displayUser.ScreenName)
                            .IgnoreResponse();
                    }
                    catch (WebApiException ex)
                    {
                        MessageBox.Show("Err:" + ex.Message + Environment.NewLine + Properties.Resources.ButtonBlockDestroy_ClickText3);
                        return;
                    }

                    MessageBox.Show(Properties.Resources.ButtonBlockDestroy_ClickText4);
                }
            }
        }

        private bool IsValidExtension(string ext)
        {
            ext = ext.ToLowerInvariant();

            return ext.Equals(".jpg", StringComparison.Ordinal) ||
                ext.Equals(".jpeg", StringComparison.Ordinal) ||
                ext.Equals(".png", StringComparison.Ordinal) ||
                ext.Equals(".gif", StringComparison.Ordinal);
        }

        private bool IsValidIconFile(FileInfo info)
        {
            return this.IsValidExtension(info.Extension) &&
                info.Length < 700 * 1024 &&
                !MyCommon.IsAnimatedGif(info.FullName);
        }

        private void ShowUserInfo_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                !e.Data.GetDataPresent(DataFormats.Html, false))  // WebBrowserコントロールからの絵文字画像D&Dは弾く
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                if (files.Length != 1)
                    return;

                var finfo = new FileInfo(files[0]);
                if (this.IsValidIconFile(finfo))
                {
                    e.Effect = DragDropEffects.Copy;
                }
            }
        }

        private async void ShowUserInfo_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                !e.Data.GetDataPresent(DataFormats.Html, false))  // WebBrowserコントロールからの絵文字画像D&Dは弾く
            {
                var ret = MessageBox.Show(this, Properties.Resources.ChangeIconToolStripMenuItem_Confirm,
                    Application.ProductName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (ret != DialogResult.OK)
                    return;

                string filename = ((string[])e.Data.GetData(DataFormats.FileDrop, false))[0];
                await this.DoChangeIcon(filename);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var cts = this.cancellationTokenSource;
                cts.Cancel();
                cts.Dispose();

                var oldImage = this.UserPicture.Image;
                this.UserPicture.Image = null;
                oldImage?.Dispose();

                this.components?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
