﻿using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BeatSaverDownloader.PluginUI
{
    internal class BeatSaverSongDetailViewController : VRUIViewController
    {
        BeatSaverMasterViewController _parentMasterViewController;
        
        private Logger log = new Logger("BeatSaverDownloader");


        TextMeshProUGUI songNameText;
        TextMeshProUGUI downloadsText;
        TextMeshProUGUI playsText;
        TextMeshProUGUI authorNameText;

        TextMeshProUGUI difficulty1Text;
        TextMeshProUGUI difficulty2Text;
        TextMeshProUGUI difficulty3Text;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            if (firstActivation)
            {
                RemoveCustomUIElements(rectTransform);
                
                RectTransform _levelDetails = GetComponentsInChildren<RectTransform>().First(x => x.name == "LevelDetails");
                _levelDetails.sizeDelta = new Vector2(44f, 20f);
                RectTransform _yourStats = GetComponentsInChildren<RectTransform>(true).First(x => x.name == "YourStats");
                _yourStats.sizeDelta = new Vector2(44f, 18f);
                _yourStats.gameObject.SetActive(true);

                TextMeshProUGUI[] _textComponents = GetComponentsInChildren<TextMeshProUGUI>();

                try
                {
                    songNameText = _textComponents.First(x => x.name == "SongNameText");

                    downloadsText = _textComponents.First(x => x.name == "DurationValueText");
                    _textComponents.First(x => x.name == "DurationText").text = "Downloads";

                    _textComponents.First(x => x.name == "BPMText").text = "Plays";
                    playsText = _textComponents.First(x => x.name == "BPMValueText");

                    _textComponents.First(x => x.name == "NotesCountText").text = "Author";
                    authorNameText = _textComponents.First(x => x.name == "NotesCountValueText");

                    authorNameText.rectTransform.sizeDelta = new Vector2(16f, 3f);
                    authorNameText.alignment = TextAlignmentOptions.CaplineRight;

                    _textComponents.First(x => x.name == "Title").text = "Difficulties";

                    _textComponents.First(x => x.name == "HighScoreText").text = "Expert/+";
                    difficulty1Text = _textComponents.First(x => x.name == "HighScoreValueText");

                    _textComponents.First(x => x.name == "MaxComboText").text = "Hard";
                    difficulty2Text = _textComponents.First(x => x.name == "MaxComboValueText");

                    _textComponents.First(x => x.name == "MaxRankText").text = "Easy/Normal";
                    _textComponents.First(x => x.name == "MaxRankText").rectTransform.sizeDelta = new Vector2(18f, 3f);
                    difficulty3Text = _textComponents.First(x => x.name == "MaxRankValueText");

                    if (_textComponents.Where(x => x.name == "ObstaclesCountText").Count() != 0)
                    {
                        Destroy(_textComponents.First(x => x.name == "ObstaclesCountText").gameObject);
                        Destroy(_textComponents.First(x => x.name == "ObstaclesCountValueText").gameObject);
                    }
                }
                catch (Exception e)
                {
                    log.Exception("EXCEPTION: " + e);
                }
            }


        }

        public void UpdateContent(Song newSongInfo)
        {
            songNameText.text = string.Format("{0}\n<size=80%>{1}</size>", HTML5Decode.HtmlDecode(newSongInfo.songName), HTML5Decode.HtmlDecode(newSongInfo.songSubName));

            downloadsText.text = HTML5Decode.HtmlDecode(newSongInfo.downloads);
            playsText.text = HTML5Decode.HtmlDecode(newSongInfo.plays);
            authorNameText.text = HTML5Decode.HtmlDecode(newSongInfo.authorName);
                        
            difficulty1Text.text = (newSongInfo.difficultyLevels.Where(x => (x.difficulty == "Expert" || x.difficulty == "ExpertPlus")).Count() > 0) ? "Yes" : "No";
            difficulty2Text.text = (newSongInfo.difficultyLevels.Where(x => x.difficulty == "Hard").Count() > 0) ? "Yes" : "No";
            difficulty3Text.text = (newSongInfo.difficultyLevels.Where(x => (x.difficulty == "Easy" || x.difficulty == "Normal")).Count() > 0) ? "Yes" : "No";
        }

        protected override void LeftAndRightScreenViewControllers(out VRUIViewController leftScreenViewController, out VRUIViewController rightScreenViewController)
        {

            if(_parentMasterViewController == null)
            {
                _parentMasterViewController = GetComponentInParent<BeatSaverMasterViewController>();
            }
            if (_parentMasterViewController._downloadQueueViewController == null)
            {
                _parentMasterViewController._downloadQueueViewController = BeatSaberUI.CreateViewController<DownloadQueueViewController>();
                _parentMasterViewController._downloadQueueViewController._parentMasterViewController = _parentMasterViewController;
            }
            leftScreenViewController = _parentMasterViewController._downloadQueueViewController;
            rightScreenViewController = null;
        }

        void RemoveCustomUIElements(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                
                if (child.name.Contains("CustomUI"))
                {
                    Destroy(child.gameObject);
                }
                if (child.childCount > 0)
                {
                    RemoveCustomUIElements(child);
                }
            }
        }

    }
}