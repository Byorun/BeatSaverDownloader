﻿using HMUI;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using SimpleJSON;
using SongLoaderPlugin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using VRUI;

namespace BeatSaverDownloader.PluginUI
{
    enum Prompt { NotSelected, Yes, No};

    class BeatSaverMasterViewController : VRUINavigationController
    {
        private Logger log = new Logger("BeatSaverDownloader");

        public BeatSaverSongListViewController _songListViewController;
        public BeatSaverSongDetailViewController _songDetailViewController;
        public SearchKeyboardViewController _searchKeyboardViewController;
        public DownloadQueueViewController _downloadQueueViewController;

        public List<Song> _songs = new List<Song>();
        public List<Song> _alreadyDownloadedSongs = new List<Song>();

        public Dictionary<string, Sprite> _cachedSprites = new Dictionary<string, Sprite>();

        private List<LevelStaticData> _notUpdatedSongs = new List<LevelStaticData>();

        public Button _downloadButton;
        Button _backButton;

        SongPreviewPlayer _songPreviewPlayer;
        public SongLoader _songLoader;

        public string _sortBy = "star";
        private bool isLoading = false;
        public bool _loading { get { return isLoading; } set { isLoading = value; SetLoadingIndicator(isLoading); } }
        public int _selectedRow = -1;
        
        Prompt _confirmDeleteState = Prompt.NotSelected;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {    
            _songLoader = FindObjectOfType<SongLoader>();
            
            _alreadyDownloadedSongs = SongLoader.CustomSongInfos.Select(x => new Song(x)).ToList();
            
            if (_songPreviewPlayer == null)
            {
                ObjectProvider[] providers = Resources.FindObjectsOfTypeAll<ObjectProvider>().Where(x => x.name == "SongPreviewPlayerProvider").ToArray();

                if (providers.Length > 0) {
                    _songPreviewPlayer = providers[0].GetProvidedObject<SongPreviewPlayer>();
                }
            }

            if (_songListViewController == null)
            {
                _songListViewController = BeatSaberUI.CreateViewController<BeatSaverSongListViewController>();
                _songListViewController.rectTransform.anchorMin = new Vector2(0.3f, 0f);
                _songListViewController.rectTransform.anchorMax = new Vector2(0.7f, 1f);

                PushViewController(_songListViewController, true);

            }
            else
            {
                if (_viewControllers.IndexOf(_songListViewController) < 0)
                {
                    PushViewController(_songListViewController, true);
                }
                 
            }
            _songListViewController.SelectTopButtons(TopButtonsState.Select);

            if (_backButton == null)
            {
                _backButton = BeatSaberUI.CreateBackButton(rectTransform);

                _backButton.onClick.AddListener(delegate ()
                {
                    if (!_loading && (_downloadQueueViewController == null || _downloadQueueViewController._queuedSongs.Count == 0))
                    {
                        if (_songPreviewPlayer != null)
                        {
                            _songPreviewPlayer.CrossfadeToDefault();
                        }
                        try
                        {
                            _songLoader.RefreshSongs();
                            _notUpdatedSongs.Clear();
                        }
                        catch (Exception e)
                        {
                            log.Exception("Can't refresh songs! EXCEPTION: " + e);
                        }
                        DismissModalViewController(null, false);
                    }
                });
            }


            GetPage(_songListViewController._currentPage);
            
        }

        protected override void DidDeactivate(DeactivationType deactivationType)
        {
            ClearSearchInput();

            base.DidDeactivate(deactivationType);
        }

        public void GetPage(int page)
        {
            
            if (IsSearching())
            {
                StartCoroutine(GetSearchResults(page, _searchKeyboardViewController._inputString));
            }
            else
            {
                StartCoroutine(GetSongs(page, _sortBy));
            }
        }

        public IEnumerator GetSongs(int page, string sortBy)
        {
            _songs.Clear();
            _songListViewController._songsTableView.ReloadData();

            _loading = true;

            UnityWebRequest www = UnityWebRequest.Get(String.Format("https://beatsaver.com/api.php?mode={0}&off={1}", sortBy, (page * _songListViewController._songsPerPage)));
            www.timeout = 10;
            yield return www.SendWebRequest();

            

            if (www.isNetworkError || www.isHttpError)
            {
                log.Error(www.error);
                TextMeshProUGUI _errorText = BeatSaberUI.CreateText(rectTransform, www.error, new Vector2(0f, -48f));
                _errorText.alignment = TextAlignmentOptions.Center;
                Destroy(_errorText.gameObject, 2f);
            }
            else
            {
                try
                {
                    string parse = "{\"songs\": " + www.downloadHandler.text + "}";

                    JSONNode node = JSON.Parse(parse);



                    for (int i = 0; i < node["songs"].Count; i++)
                    {
                        _songs.Add(new Song(node["songs"][i]));
                    }


                    _songListViewController._songsTableView.ReloadData();
                    if (_selectedRow != -1 && _songs.Count > 0)
                    {
                        _songListViewController._songsTableView.SelectRow(Math.Min(_selectedRow, _songs.Count-1));
                        ShowDetails(Math.Min(_selectedRow, _songs.Count-1));
                    }

                    _songListViewController._pageUpButton.interactable = (page == 0) ? false : true;
                    _songListViewController._pageDownButton.interactable = (_songs.Count < _songListViewController._songsPerPage) ? false : true;

                }
                catch (Exception e)
                {
                    log.Exception("EXCEPTION(GET SONGS): " + e);
                }
            }
            _loading = false;
        }

        public IEnumerator GetSearchResults(int page, string search)
        {
            _songs.Clear();
            _songListViewController._songsTableView.ReloadData();

            UnityWebRequest www = UnityWebRequest.Get(String.Format("https://beatsaver.com/search.php?q={0}", search));
            www.timeout = 10;
            yield return www.SendWebRequest();

            

            if (www.isNetworkError || www.isHttpError)
            {
                log.Error(www.error);
                TextMeshProUGUI _errorText = BeatSaberUI.CreateText(rectTransform, www.error, new Vector2(0f, -48f));
                _errorText.alignment = TextAlignmentOptions.Center;
                Destroy(_errorText.gameObject, 2f);
            }
            else
            {
                try
                {
                    string parse = www.downloadHandler.text;

                    JSONNode node = JSON.Parse(parse);

                    for (int i = (page * _songListViewController._songsPerPage); i < Math.Min(node["hits"]["hits"].Count, ((page + 1) * _songListViewController._songsPerPage)); i++)
                    {
                        
                        _songs.Add(new Song(node["hits"]["hits"][i]["_source"], JSON.Parse(node["hits"]["hits"][i]["_source"]["difficultyLevels"].Value)));
                    }
                    
                    _songListViewController._songsTableView.ReloadData();
                    if (_selectedRow != -1 && _songs.Count > 0)
                    {
                        _songListViewController._songsTableView.SelectRow(Math.Min(_selectedRow,_songs.Count-1));
                        ShowDetails(Math.Min(_selectedRow, _songs.Count-1));
                    }

                    _songListViewController._pageUpButton.interactable = (page == 0) ? false : true;
                    _songListViewController._pageDownButton.interactable = (_songs.Count < _songListViewController._songsPerPage) ? false : true;

                }
                catch (Exception e)
                {
                    log.Exception("EXCEPTION(GET SEARCH RESULTS): " + e);
                }
            }
            _loading = false;
        }

        public void DownloadSong(int buttonId)
        {
            log.Log("Downloading "+_songs[buttonId].beatname);

            if (!_downloadQueueViewController._queuedSongs.Contains(_songs[buttonId]))
            {
                _downloadQueueViewController.EnqueueSong(_songs[buttonId]);
            }
        }

        public IEnumerator DownloadSongCoroutine(Song songInfo)
        {
            if(_songs[_selectedRow].Compare(songInfo))
            {
                RefreshDetails(_selectedRow);
            }

            string downloadedSongPath = "";

            UnityWebRequest www = UnityWebRequest.Get("https://beatsaver.com/dl.php?id=" + (songInfo.id));
            www.timeout = 10;
            yield return www.SendWebRequest();

            log.Log("Received response from BeatSaver.com...");

            if (www.isNetworkError || www.isHttpError)
            {
                log.Error(www.error);
                TextMeshProUGUI _errorText = BeatSaberUI.CreateText(_songDetailViewController.rectTransform, www.error, new Vector2(18f, -64f));
                Destroy(_errorText.gameObject, 2f);
            }
            else
            {
                string zipPath = "";
                string docPath = "";
                string customSongsPath = "";
                try
                {
                    byte[] data = www.downloadHandler.data;

                    docPath = Application.dataPath;
                    docPath = docPath.Substring(0, docPath.Length - 5);
                    docPath = docPath.Substring(0, docPath.LastIndexOf("/"));
                    customSongsPath = docPath + "/CustomSongs/" + songInfo.id +"/";
                    zipPath = customSongsPath + songInfo.beatname + ".zip";
                    if (!Directory.Exists(customSongsPath)) {
                        Directory.CreateDirectory(customSongsPath);
                    }
                    File.WriteAllBytes(zipPath, data);
                    log.Log("Downloaded zip file!");
                }catch(Exception e)
                {
                    log.Exception("EXCEPTION: "+e);
                    yield break;
                }
                

                FastZip zip = new FastZip();

                log.Log("Extracting...");
                zip.ExtractZip(zipPath, customSongsPath, null);

                if (Directory.GetDirectories(customSongsPath).Length > 0)
                {
                    downloadedSongPath = Directory.GetDirectories(customSongsPath)[0];
                } 

                try
                {
                    CustomSongInfo downloadedSong = GetCustomSongInfo(downloadedSongPath);
                    
                    _alreadyDownloadedSongs.Add(new Song(downloadedSong));

                    CustomLevelStaticData newLevel = null;
                    try
                    {
                        newLevel = ScriptableObject.CreateInstance<CustomLevelStaticData>();
                    }
                    catch (Exception e)
                    {
                        //LevelStaticData.OnEnable throws null reference exception because we don't have time to set _difficultyLevels
                    }

                    ReflectionUtil.SetPrivateField(newLevel, "_levelId", downloadedSong.GetIdentifier());
                    ReflectionUtil.SetPrivateField(newLevel, "_authorName", downloadedSong.authorName);
                    ReflectionUtil.SetPrivateField(newLevel, "_songName", downloadedSong.songName);
                    ReflectionUtil.SetPrivateField(newLevel, "_songSubName", downloadedSong.songSubName);
                    ReflectionUtil.SetPrivateField(newLevel, "_previewStartTime", downloadedSong.previewStartTime);
                    ReflectionUtil.SetPrivateField(newLevel, "_previewDuration", downloadedSong.previewDuration);
                    ReflectionUtil.SetPrivateField(newLevel, "_beatsPerMinute", downloadedSong.beatsPerMinute);

                    StartCoroutine(LoadAudio("file://" + downloadedSong.path + "/" + downloadedSong.audioPath, newLevel, "audioClip"));

                    newLevel.OnEnable();
                    _notUpdatedSongs.Add(newLevel);
                }catch(Exception e)
                {
                    log.Exception("Can't play preview! Exception: "+e);
                }


                log.Log("Downloaded!");
                File.Delete(zipPath);

                
                _songListViewController._songsTableView.ReloadData();
                _songListViewController._songsTableView.SelectRow(_selectedRow);
            }

            if (_songs[_selectedRow].Compare(songInfo))
            {
                RefreshDetails(_selectedRow);
            }

        }

        IEnumerator DeleteSong(Song _songInfo)
        {
            bool zippedSong = false;
            _loading = true;
            _downloadButton.interactable = false;

            string _songPath = GetDownloadedSongPath(_songInfo);

            if (!string.IsNullOrEmpty(_songPath) && _songPath.Contains("/.cache/"))
            {
                zippedSong = true;
            }

            if (string.IsNullOrEmpty(_songPath))
            {
                log.Error("Song path is null or empty!");
                _loading = false;
                _downloadButton.interactable = true;
                yield break;
            }
            if (!Directory.Exists(_songPath))
            {
                log.Error("Song folder does not exists!");
                _loading = false;
                _downloadButton.interactable = true;
                yield break;
            }

            yield return PromptDeleteFolder(_songPath);

            if(_confirmDeleteState == Prompt.Yes)
            {
                if (zippedSong)
                {
                    log.Log("Deleting \"" + _songPath.Substring(_songPath.LastIndexOf('/')) + "\"...");
                    Directory.Delete(_songPath, true);

                    string songHash = Directory.GetParent(_songPath).Name;

                    if (Directory.GetFileSystemEntries(_songPath.Substring(0, _songPath.LastIndexOf('/'))).Length == 0)
                    {
                        log.Log("Deleting empty folder \"" + _songPath.Substring(0, _songPath.LastIndexOf('/')) + "\"...");
                        Directory.Delete(_songPath.Substring(0, _songPath.LastIndexOf('/')), false);
                    }

                    string docPath = Application.dataPath;
                    docPath = docPath.Substring(0, docPath.Length - 5);
                    docPath = docPath.Substring(0, docPath.LastIndexOf("/"));
                    string customSongsPath = docPath + "/CustomSongs/";

                    string hash = "";

                    foreach (string file in Directory.GetFiles(customSongsPath, "*.zip"))
                    {
                        if(PluginUI.CreateMD5FromFile(file,out hash))
                        {
                            if (hash == songHash)
                            {
                                File.Delete(file);
                                break;
                            }
                        }
                    }

                }
                else
                {
                    log.Log("Deleting \"" + _songPath.Substring(_songPath.LastIndexOf('/')) + "\"...");
                    Directory.Delete(_songPath, true);
                    if (Directory.GetFileSystemEntries(_songPath.Substring(0, _songPath.LastIndexOf('/'))).Length == 0)
                    {
                        log.Log("Deleting empty folder \"" + _songPath.Substring(0, _songPath.LastIndexOf('/')) + "\"...");
                        Directory.Delete(_songPath.Substring(0, _songPath.LastIndexOf('/')), false);
                    }
                }
            }
            _confirmDeleteState = Prompt.NotSelected;



            log.Log($"{_alreadyDownloadedSongs.RemoveAll(x => x.Compare(_songInfo))} song removed");


            _songListViewController._songsTableView.ReloadData();
            _songListViewController._songsTableView.SelectRow(_selectedRow);
            RefreshDetails(_selectedRow);

            _loading = false;
            _downloadButton.interactable = true;
        }


        IEnumerator PromptDeleteFolder(string dirName)
        {
            TextMeshProUGUI _deleteText = BeatSaberUI.CreateText(_songDetailViewController.rectTransform, String.Format("Delete folder \"{0}\"?", dirName.Substring(dirName.LastIndexOf('/')).Trim('/')), new Vector2(18f, -64f));

            Button _confirmDelete = BeatSaberUI.CreateUIButton(_songDetailViewController.rectTransform, "ApplyButton");

            BeatSaberUI.SetButtonText(ref _confirmDelete, "Yes");
            (_confirmDelete.transform as RectTransform).sizeDelta = new Vector2(15f, 10f);
            (_confirmDelete.transform as RectTransform).anchoredPosition = new Vector2(-13f, 6f);
            _confirmDelete.onClick.AddListener(delegate () { _confirmDeleteState = Prompt.Yes; });

            Button _discardDelete = BeatSaberUI.CreateUIButton(_songDetailViewController.rectTransform, "ApplyButton");

            BeatSaberUI.SetButtonText(ref _discardDelete, "No");
            (_discardDelete.transform as RectTransform).sizeDelta = new Vector2(15f, 10f);
            (_discardDelete.transform as RectTransform).anchoredPosition = new Vector2(2f, 6f);
            _discardDelete.onClick.AddListener(delegate () { _confirmDeleteState = Prompt.No; });


            (_downloadButton.transform as RectTransform).anchoredPosition = new Vector2(2f, -10f);
            
            yield return new WaitUntil(delegate () { return (_confirmDeleteState == Prompt.Yes || _confirmDeleteState == Prompt.No); });

            (_downloadButton.transform as RectTransform).anchoredPosition = new Vector2(2f, 6f);

            Destroy(_deleteText.gameObject);
            Destroy(_confirmDelete.gameObject);
            Destroy(_discardDelete.gameObject);
            
        }

        public void ClearSearchInput()
        {
            if(_searchKeyboardViewController != null)
            {
                _searchKeyboardViewController._inputString = "";
            }
        }

        public bool IsSearching()
        {
            return (_searchKeyboardViewController != null && !String.IsNullOrEmpty(_searchKeyboardViewController._inputString));
        }

        public void ShowSearchKeyboard()
        {
            if (_searchKeyboardViewController == null)
            {
                _searchKeyboardViewController = BeatSaberUI.CreateViewController<SearchKeyboardViewController>();
                PresentModalViewController(_searchKeyboardViewController, null);
                
            }
            else
            {
                PresentModalViewController(_searchKeyboardViewController, null);
                
            }
        }

        void SetLoadingIndicator(bool loading)
        {
            if(_songListViewController != null && _songListViewController._loadingIndicator)
            {
                _songListViewController._loadingIndicator.SetActive(loading);
            }
        }

        public void ShowDetails(int row)
        {
            _selectedRow = row;
            
            if (_songDetailViewController == null)
            {
                GameObject _songDetailGameObject = Instantiate(Resources.FindObjectsOfTypeAll<SongDetailViewController>().First(), rectTransform, false).gameObject;
                Destroy(_songDetailGameObject.GetComponent<SongDetailViewController>());
                _songDetailViewController = _songDetailGameObject.AddComponent<BeatSaverSongDetailViewController>();
                
                PushViewController(_songDetailViewController, false);
                RefreshDetails(row);
            }
            else
            {
                if (_viewControllers.IndexOf(_songDetailViewController) < 0)
                {
                    PushViewController(_songDetailViewController, true);
                    RefreshDetails(row);
                }
                else
                {
                    RefreshDetails(row);
                }
                
            }
        }

        private void RefreshDetails(int row)
        {
            if(_songs.Count<=row)
            {
                return;
            }

            _songDetailViewController.UpdateContent(_songs[row]);
            
            if (_downloadButton == null)
            {
                _downloadButton = _songDetailViewController.GetComponentInChildren<Button>();
                (_downloadButton.transform as RectTransform).sizeDelta = new Vector2(30f, 10f);
                (_downloadButton.transform as RectTransform).anchoredPosition = new Vector2(2f, 6f);

            }

            if (IsSongAlreadyDownloaded(_songs[row]))
            {
                BeatSaberUI.SetButtonText(ref _downloadButton, "Delete");

                _downloadButton.onClick.RemoveAllListeners();

                _downloadButton.onClick.AddListener(delegate ()
                {
                    if (!_loading)
                    {
                        StartCoroutine(DeleteSong(_songs[row]));
                    }

                });

                LevelStaticData _songData = GetLevelStaticDataForSong(_songs[row]);
                
                PlayPreview(_songData);
                

                string _songPath = GetDownloadedSongPath(_songs[row]);
                
                if (string.IsNullOrEmpty(_songPath))
                {
                    _downloadButton.interactable = false;
                }
                else
                {
                    _downloadButton.interactable = true;
                }
            }
            else
            {
                BeatSaberUI.SetButtonText(ref _downloadButton, "Download");
                _downloadButton.interactable = true;
                
                _downloadButton.onClick.RemoveAllListeners();

                _downloadButton.onClick.AddListener(delegate ()
                {
                    if (!_loading)
                    {
                        DownloadSong(row);
                    }

                });

                if (_songPreviewPlayer != null)
                {
                    _songPreviewPlayer.CrossfadeToDefault();
                }
            }

            if (_downloadQueueViewController != null && _downloadQueueViewController._queuedSongs.Contains(_songs[row]) && !IsSongAlreadyDownloaded(_songs[row]))
            {
                BeatSaberUI.SetButtonText(ref _downloadButton, "Queued...");
                _downloadButton.interactable = false;
            }
        }

        void PlayPreview(LevelStaticData _songData)
        {
            if (_songData != null)
            {
                log.Log("Playing preview for " + _songData.songName);
                if (_songData.audioClip != null)
                {
                    if (_songPreviewPlayer != null && _songData != null)
                    {
                        try
                        {
                            _songPreviewPlayer.CrossfadeTo(_songData.audioClip, _songData.previewStartTime, _songData.previewDuration, 1f);
                        }
                        catch (Exception e)
                        {
                            log.Error("Can't play preview! Exception: " + e);
                        }
                    }
                }
                else
                {
                    StartCoroutine(PlayPreviewCoroutine(_songData));
                }
            }
            else
            {
                log.Error($"PLAY PREVIEW: SongData is null!");
            }
        }

        IEnumerator PlayPreviewCoroutine(LevelStaticData _songData)
        {
            yield return new WaitWhile(delegate () { return _songData.audioClip != null; });

            if (_songPreviewPlayer != null && _songData != null && _songData.audioClip != null)
            {
                try
                {
                    _songPreviewPlayer.CrossfadeTo(_songData.audioClip, _songData.previewStartTime, _songData.previewDuration, 1f);
                }
                catch (Exception e)
                {
                    log.Error("Can't play preview! Exception: " + e);
                }
            }
        }

        public IEnumerator LoadSprite(string spritePath, TableCell obj)
        {
            Texture2D tex;

            if (_cachedSprites.ContainsKey(spritePath))
            {
                obj.GetComponentsInChildren<UnityEngine.UI.Image>()[2].sprite = _cachedSprites[spritePath];
                yield break;
            }

            using (WWW www = new WWW(spritePath))
            {
                yield return www;
                tex = www.texture;
                var newSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f, 100, 1);
                _cachedSprites.Add(spritePath, newSprite);
                obj.GetComponentsInChildren<UnityEngine.UI.Image>()[2].sprite = newSprite;
            }
        }        

        private IEnumerator LoadAudio(string audioPath, object obj, string fieldName)
        {
            using (var www = new WWW(audioPath))
            {
                yield return www;
                ReflectionUtil.SetPrivateField(obj, fieldName, www.GetAudioClip(true, true, AudioType.UNKNOWN));
            }
        }

        public bool IsSongAlreadyDownloaded(Song _song)
        {
            bool alreadyDownloaded = false;

            foreach (Song song in _alreadyDownloadedSongs)
            {
                alreadyDownloaded = alreadyDownloaded || song.Compare(_song);
            }

            return alreadyDownloaded;
        }

        public LevelStaticData GetLevelStaticDataForSong(Song _song)
        {
            foreach(LevelCollectionsForGameplayModes.LevelCollectionForGameplayMode levelCollection in PluginUI._levelCollectionsForGameModes)
            {
                foreach(LevelStaticData data in levelCollection.levelCollection.levelsData)
                {
                    if ((new Song(data)).Compare(_song))
                    {
                        return data;
                    }
                }
            }
            
            foreach (CustomLevelStaticData data in _notUpdatedSongs)
            {
                if ((new Song(data)).Compare(_song))
                {
                    return data;
                }
            }
            return null;
        }

        public string GetDownloadedSongPath(Song _song)
        {
            foreach (Song song in _alreadyDownloadedSongs)
            {
                if (song.Compare(_song))
                {
                    return song.path;
                }
            }

            return null;
        }

        private CustomSongInfo GetCustomSongInfo(string _songPath)
        {
            string songPath = _songPath;
            if(songPath.Contains("/autosaves"))
            {
                songPath = songPath.Replace("/autosaves","");
            }
            var infoText = File.ReadAllText(songPath + "/info.json");
            CustomSongInfo songInfo;
            try
            {
                songInfo = JsonUtility.FromJson<CustomSongInfo>(infoText);
            }
            catch (Exception e)
            {
                log.Warning("Error parsing song: " + songPath);
                return null;
            }
            songInfo.path = songPath;
            
            var diffLevels = new List<CustomSongInfo.DifficultyLevel>();
            var n = JSON.Parse(infoText);
            var diffs = n["difficultyLevels"];
            for (int i = 0; i < diffs.AsArray.Count; i++)
            {
                n = diffs[i];
                diffLevels.Add(new CustomSongInfo.DifficultyLevel()
                {
                    difficulty = n["difficulty"],
                    difficultyRank = n["difficultyRank"].AsInt,
                    audioPath = n["audioPath"],
                    jsonPath = n["jsonPath"]
                });
            }
            songInfo.difficultyLevels = diffLevels.ToArray();
            return songInfo;
        }

    }
}
