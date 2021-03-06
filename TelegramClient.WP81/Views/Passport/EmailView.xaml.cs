﻿// 
// This is the source code of Telegram for Windows Phone v. 3.x.x.
// It is licensed under GNU GPL v. 2 or later.
// You should have received a copy of the license in this archive (see LICENSE).
// 
// Copyright Evgeny Nadymov, 2013-present.
// 

using System.Windows.Input;
using TelegramClient.ViewModels.Passport;

namespace TelegramClient.Views.Passport
{
    public partial class EmailView
    {
        public EmailViewModel ViewModel
        {
            get { return DataContext as EmailViewModel; }
        }

        public EmailView()
        {
            InitializeComponent();

            Caption.Background = ShellView.CaptionBrush;
        }

        private void DoneIcon_OnTap(object sender, GestureEventArgs e)
        {
            ViewModel.Done();
        }
    }
}