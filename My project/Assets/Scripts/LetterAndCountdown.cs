using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;    
using UnityEngine.Video;
public class LetterAndCountdown : MonoBehaviour
{
    [Header("화면 장치 연결")]
    public TextMeshProUGUI letterText;      // 글자 텍스트
    public TextMeshProUGUI countdownText;   // 카운트다운
    public RawImage displayScreen;          // 화면(하얀 박스)
    public VideoPlayer videoPlayer;         // 비디오 재생기

    [Header("데이터 리스트 (여기에 파일을 끌어다 놓으세요)")]
    public List<LetterData> contentList; 

    // 데이터 꾸러미 정의
    [System.Serializable]
    public struct LetterData 
    { 
        public string letter;       // 글자 (예: ㄱ, ㄲ, ㅏ)
        public Texture imageFile;   // 이미지 파일 (png, jpg)
        public VideoClip videoFile; // 동영상 파일 (mov, mp4)
    }

    // 비디오용 임시 화면 텍스처
    private RenderTexture _videoTexture;

    void Start()
    {
        // 1. 비디오용 스크린을 코드로 생성 (메모리 최적화)
        _videoTexture = new RenderTexture(1920, 1080, 24);
        videoPlayer.targetTexture = _videoTexture;

        StartCoroutine(LetterRoutine());
        StartCoroutine(CountdownRoutine());
    }

    IEnumerator LetterRoutine()
    {
        List<string> letters = LetterSetManager.selectedLetters;

        for (int i = 0; i < letters.Count; i++)
        {
            string currentLetter = letters[i];
            letterText.text = currentLetter;

            // 리스트에서 현재 글자에 맞는 데이터 찾기 ---
            // 리스트를 뒤져서 'letter'가 'currentLetter'와 같은 항목을 찾습니다.
            LetterData data = contentList.Find(x => x.letter == currentLetter);

            // 데이터가 존재한다면?
            if (data.letter != null) 
            {
                displayScreen.enabled = true; // 화면 켜기

                // 우선순위 로직
                // 1. 비디오 파일이 설정되어 있다면? -> 비디오 재생
                if (data.videoFile != null) 
                {
                    videoPlayer.clip = data.videoFile; // mov, mp4 상관없이 재생
                    displayScreen.texture = _videoTexture; // 화면을 비디오 화면으로 전환
                    videoPlayer.Play();
                }
                // 2. 비디오는 없고 이미지만 있다면? -> 이미지 표시
                else if (data.imageFile != null) 
                {
                    videoPlayer.Stop(); // 비디오 끄기
                    displayScreen.texture = data.imageFile; // 화면을 이미지로 전환
                }
            }
            // -----------------------------------------------------

            yield return new WaitForSeconds(3f);
        }

        letterText.text = "";
        SceneManager.LoadScene("result1");
    }

    IEnumerator CountdownRoutine()
    {
        while (true)
        {
            countdownText.text = "3";
            yield return new WaitForSeconds(1f);
            countdownText.text = "2";
            yield return new WaitForSeconds(1f);
            countdownText.text = "1";
            yield return new WaitForSeconds(1f);
        }
    }
}