﻿using BeatSaverDownloader.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BeatSaverDownloader.PluginUI
{
    class DownloadQueueTableCell : SongListTableCell
    {

        Song song;


        protected override void Awake()
        {
            Logger.StaticLog("AWAKE");

            base.Awake();
        }

        public void Init(Song _song)
        {
            SongListTableCell cell = GetComponent<SongListTableCell>();
            
            foreach (FieldInfo info in cell.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                info.SetValue(this, info.GetValue(cell));
            }
            
            Destroy(cell);
            
            song = _song;
            
            songName = string.Format("{0}\n<size=80%>{1}</size>", HTML5Decode.HtmlDecode(song.songName), HTML5Decode.HtmlDecode(song.songSubName));
            author = HTML5Decode.HtmlDecode(song.authorName);
            StartCoroutine(LoadScripts.LoadSprite("https://beatsaver.com/img/" + song.id + "." + song.img, this));

            _bgImage.enabled = true;
            _bgImage.sprite = Sprite.Create((new Texture2D(1, 1)), new Rect(0, 0, 1, 1), Vector2.one / 2f);
            _bgImage.type = UnityEngine.UI.Image.Type.Filled;
            _bgImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _bgImage.fillAmount = song.downloadingProgress;
            _bgImage.color = new Color(1f, 1f, 1f, 0.35f);
        }

        public void Update()
        {

            _bgImage.enabled = true;
            _bgImage.fillAmount = song.downloadingProgress;
        }

    }
}
