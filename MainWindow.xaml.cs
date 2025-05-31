using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AudioPlayer
{
    public partial class MainWindow : Window
    {
        private MediaPlayer mediaPlayer;
        private DispatcherTimer timer;
        private bool isPlaying = false;
        private bool isDragging = false;
        private ObservableCollection<AudioFile> playlist;
        private int currentIndex = -1;
        private PlayMode playMode = PlayMode.Normal;
        private bool isShuffleMode = false;
        private Random random = new Random();

        public MainWindow()
        {
            InitializeComponent();
            InitializePlayer();
        }

        private void InitializePlayer()
        {
            mediaPlayer = new MediaPlayer();
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += Timer_Tick;

            playlist = new ObservableCollection<AudioFile>();
            lstPlaylist.ItemsSource = playlist;

            // 设置初始音量
            mediaPlayer.Volume = 0.5;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer.Source != null && !isDragging)
            {
                if (mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    var position = mediaPlayer.Position;
                    var duration = mediaPlayer.NaturalDuration.TimeSpan;

                    txtCurrentTime.Text = FormatTime(position);
                    txtTotalTime.Text = FormatTime(duration);

                    if (duration.TotalSeconds > 0)
                    {
                        sliderPosition.Value = (position.TotalSeconds / duration.TotalSeconds) * 100;
                    }
                }
            }
        }

        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "音频文件|*.mp3;*.wav;*.wma;*.m4a;*.aac;*.flac|所有文件|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string fileName in openFileDialog.FileNames)
                {
                    AddFileToPlaylist(fileName);
                }
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            // 使用WPF的文件夹选择对话框替代方案
            OpenFileDialog openFolderDialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "选择文件夹",
                Filter = "文件夹|*.folder",
                Title = "选择包含音频文件的文件夹"
            };

            if (openFolderDialog.ShowDialog() == true)
            {
                string selectedPath = Path.GetDirectoryName(openFolderDialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath) && Directory.Exists(selectedPath))
                {
                    string[] audioExtensions = { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac" };
                    var audioFiles = Directory.GetFiles(selectedPath, "*.*", SearchOption.AllDirectories)
                        .Where(file => audioExtensions.Contains(Path.GetExtension(file).ToLower()));

                    foreach (string file in audioFiles)
                    {
                        AddFileToPlaylist(file);
                    }
                }
                else
                {
                    // 简单的手动输入文件夹路径的方式
                    string folderPath = Microsoft.VisualBasic.Interaction.InputBox(
                        "请输入文件夹路径:", "选择文件夹", "C:\\Music");

                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        string[] audioExtensions = { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac" };
                        var audioFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                            .Where(file => audioExtensions.Contains(Path.GetExtension(file).ToLower()));

                        foreach (string file in audioFiles)
                        {
                            AddFileToPlaylist(file);
                        }
                    }
                }
            }
        }

        private void AddFileToPlaylist(string filePath)
        {
            if (File.Exists(filePath))
            {
                var audioFile = new AudioFile
                {
                    FilePath = filePath,
                    FileName = Path.GetFileNameWithoutExtension(filePath),
                    Duration = GetAudioDuration(filePath)
                };
                playlist.Add(audioFile);
            }
        }

        private string GetAudioDuration(string filePath)
        {
            try
            {
                var tempPlayer = new MediaPlayer();
                tempPlayer.Open(new Uri(filePath));

                // 这里需要等待媒体打开，实际应用中可能需要更复杂的处理
                System.Threading.Thread.Sleep(1000);

                if (tempPlayer.NaturalDuration.HasTimeSpan)
                {
                    var duration = tempPlayer.NaturalDuration.TimeSpan;
                    tempPlayer.Close();
                    return FormatTime(duration);
                }
                tempPlayer.Close();
            }
            catch
            {
                // 忽略错误
            }
            return "未知";
        }

        private void BtnClearPlaylist_Click(object sender, RoutedEventArgs e)
        {
            playlist.Clear();
            mediaPlayer.Stop();
            currentIndex = -1;
            txtCurrentFile.Text = "未选择文件";
            txtFileInfo.Text = "";
        }

        private void LstPlaylist_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstPlaylist.SelectedIndex >= 0)
            {
                currentIndex = lstPlaylist.SelectedIndex;
                UpdateCurrentFileInfo();
            }
        }

        private void LstPlaylist_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstPlaylist.SelectedIndex >= 0)
            {
                currentIndex = lstPlaylist.SelectedIndex;
                PlayCurrentFile();
            }
        }

        private void UpdateCurrentFileInfo()
        {
            if (currentIndex >= 0 && currentIndex < playlist.Count)
            {
                var currentFile = playlist[currentIndex];
                txtCurrentFile.Text = currentFile.FileName;

                try
                {
                    var fileInfo = new FileInfo(currentFile.FilePath);
                    txtFileInfo.Text = $"大小: {fileInfo.Length / 1024 / 1024:F1} MB\n" +
                                      $"修改时间: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm}";
                }
                catch
                {
                    txtFileInfo.Text = "无法获取文件信息";
                }
            }
        }

        private void PlayCurrentFile()
        {
            if (currentIndex >= 0 && currentIndex < playlist.Count)
            {
                try
                {
                    var currentFile = playlist[currentIndex];
                    mediaPlayer.Open(new Uri(currentFile.FilePath));
                    mediaPlayer.Play();
                    isPlaying = true;
                    btnPlayPause.Content = "⏸";
                    timer.Start();
                    lstPlaylist.SelectedIndex = currentIndex;
                    UpdateCurrentFileInfo();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"播放文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.Source == null && playlist.Count > 0)
            {
                if (currentIndex < 0) currentIndex = 0;
                PlayCurrentFile();
            }
            else if (isPlaying)
            {
                mediaPlayer.Pause();
                isPlaying = false;
                btnPlayPause.Content = "▶";
                timer.Stop();
            }
            else
            {
                mediaPlayer.Play();
                isPlaying = true;
                btnPlayPause.Content = "⏸";
                timer.Start();
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            isPlaying = false;
            btnPlayPause.Content = "▶";
            timer.Stop();
            sliderPosition.Value = 0;
            txtCurrentTime.Text = "00:00";
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (playlist.Count == 0) return;

            if (isShuffleMode)
            {
                currentIndex = random.Next(playlist.Count);
            }
            else
            {
                currentIndex--;
                if (currentIndex < 0)
                {
                    currentIndex = playlist.Count - 1;
                }
            }
            PlayCurrentFile();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            PlayNextSong();
        }

        private void PlayNextSong()
        {
            if (playlist.Count == 0) return;

            if (isShuffleMode)
            {
                currentIndex = random.Next(playlist.Count);
            }
            else
            {
                currentIndex++;
                if (currentIndex >= playlist.Count)
                {
                    if (playMode == PlayMode.RepeatAll)
                    {
                        currentIndex = 0;
                    }
                    else
                    {
                        return; // 播放结束
                    }
                }
            }
            PlayCurrentFile();
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Volume = sliderVolume.Value / 100.0;
                txtVolumeValue.Text = $"{(int)sliderVolume.Value}%";
            }
        }

        private void SliderSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.SpeedRatio = sliderSpeed.Value;
                txtSpeedValue.Text = $"{sliderSpeed.Value:F1}x";
            }
        }

        private void SliderPosition_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
        }

        private void SliderPosition_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var duration = mediaPlayer.NaturalDuration.TimeSpan;
                var newPosition = TimeSpan.FromSeconds((sliderPosition.Value / 100.0) * duration.TotalSeconds);
                mediaPlayer.Position = newPosition;
            }
            isDragging = false;
        }

        private void SliderBass_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            txtBassValue.Text = ((int)sliderBass.Value).ToString();
            // 注意: WPF的MediaPlayer不直接支持均衡器，这里只是UI演示
            // 实际应用需要使用专门的音频库如NAudio
        }

        private void SliderTreble_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            txtTrebleValue.Text = ((int)sliderTreble.Value).ToString();
            // 同上，需要专门的音频库支持
        }

        private void BtnPlayMode_Click(object sender, RoutedEventArgs e)
        {
            switch (playMode)
            {
                case PlayMode.Normal:
                    playMode = PlayMode.RepeatAll;
                    btnPlayMode.Content = "🔁";
                    btnPlayMode.ToolTip = "循环播放";
                    break;
                case PlayMode.RepeatAll:
                    playMode = PlayMode.RepeatOne;
                    btnPlayMode.Content = "🔂";
                    btnPlayMode.ToolTip = "单曲循环";
                    break;
                case PlayMode.RepeatOne:
                    playMode = PlayMode.Normal;
                    btnPlayMode.Content = "▶️";
                    btnPlayMode.ToolTip = "顺序播放";
                    break;
            }
        }

        private void BtnShuffle_Click(object sender, RoutedEventArgs e)
        {
            isShuffleMode = !isShuffleMode;
            btnShuffle.Background = isShuffleMode ?
                new SolidColorBrush(Color.FromRgb(0, 160, 255)) :
                new SolidColorBrush(Color.FromRgb(64, 64, 64));
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            if (playMode == PlayMode.RepeatOne)
            {
                mediaPlayer.Position = TimeSpan.Zero;
                mediaPlayer.Play();
            }
            else
            {
                PlayNextSong();
            }
        }

        private void MediaPlayer_MediaOpened(object sender, EventArgs e)
        {
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                txtTotalTime.Text = FormatTime(mediaPlayer.NaturalDuration.TimeSpan);
            }
        }

        private void MediaPlayer_MediaFailed(object sender, ExceptionEventArgs e)
        {
            MessageBox.Show($"媒体播放失败: {e.ErrorException.Message}", "错误",
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (File.Exists(file))
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string[] audioExtensions = { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac" };
                        if (audioExtensions.Contains(extension))
                        {
                            AddFileToPlaylist(file);
                        }
                    }
                    else if (Directory.Exists(file))
                    {
                        // 处理拖拽文件夹
                        string[] audioExtensions = { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac" };
                        var audioFiles = Directory.GetFiles(file, "*.*", SearchOption.AllDirectories)
                            .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLower()));

                        foreach (string audioFile in audioFiles)
                        {
                            AddFileToPlaylist(audioFile);
                        }
                    }
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            mediaPlayer?.Stop();
            mediaPlayer?.Close();
            timer?.Stop();
            base.OnClosed(e);
        }
    }

    public class AudioFile
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Duration { get; set; }
    }

    public enum PlayMode
    {
        Normal,      // 顺序播放
        RepeatAll,   // 循环播放
        RepeatOne    // 单曲循环
    }
}