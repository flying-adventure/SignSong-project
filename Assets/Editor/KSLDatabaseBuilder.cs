using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// NIA 수어 데이터셋을 기반으로
/// - SignClip.asset 들 자동 생성
/// - SignDatabase.asset 자동 생성
/// 하는 에디터 유틸리티.
/// 
/// 기대하는 폴더 구조:
/// Assets/SignDataset/Keypoints/REAL17_F/NIA_SL_WORD0001_REAL17_F/*.json
/// Assets/SignDataset/Morpheme/REAL17_F/NIA_SL_WORD0001_REAL17_F_morpheme.json
/// </summary>
public static class KSLDatabaseBuilder
{
    // 키포인트 / 모포임 루트 경로 (Unity 프로젝트 상대 경로)
    private const string KEYPOINT_ROOT = "Assets/SignDataset/Keypoints";
    private const string MORPHEME_ROOT = "Assets/SignDataset/Morpheme";
    private const string CLIP_ASSET_FOLDER = "Assets/SignDataset/Clips";
    private const string DATABASE_ASSET_PATH = "Assets/SignDataset/SignDatabase.asset";

    [MenuItem("KSL/Build Sign Database (from SignDataset)")]
    public static void BuildDatabaseFromDataset()
    {
        // Application.dataPath 는 /.../Assets 절대경로
        string projectAssetsPath = Application.dataPath.Replace("\\", "/"); // …/YourProject/Assets

        // 1. Keypoints 루트 폴더 존재 여부 체크
        string keyAbsRoot = Path.Combine(projectAssetsPath, "SignDataset/Keypoints").Replace("\\", "/");
        if (!Directory.Exists(keyAbsRoot))
        {
            Debug.LogError($"[KSLDatabaseBuilder] 키포인트 루트 폴더가 없습니다: {keyAbsRoot}");
            return;
        }

        // 2. Clips 폴더 없으면 생성
        EnsureFolder("Assets/SignDataset");
        EnsureFolder(CLIP_ASSET_FOLDER);

        var createdClips = new List<SignClip>();

        // 3. Keypoints 아래의 세트 폴더들 (예: REAL17_F, REAL17_D ...)
        string[] setDirs = Directory.GetDirectories(keyAbsRoot);
        foreach (var setDirAbs in setDirs)
        {
            string setName = Path.GetFileName(setDirAbs); // REAL17_F

            // 각 세트(REAL17_F) 안의 단어 폴더 (예: NIA_SL_WORD0001_REAL17_F)
            string[] signDirs = Directory.GetDirectories(setDirAbs);
            foreach (var signDirAbs in signDirs)
            {
                string signFolderName = Path.GetFileName(signDirAbs); // NIA_SL_WORD0001_REAL17_F

                // 3-1. 이 폴더 안의 .json 프레임들 정렬해서 TextAsset 배열로 만들기
                var frames = LoadKeypointFrames(projectAssetsPath, signDirAbs);
                if (frames == null || frames.Length == 0)
                {
                    Debug.LogWarning($"[KSLDatabaseBuilder] 키포인트 프레임이 없습니다: {signDirAbs}");
                    continue;
                }

                // 3-2. 모포임 JSON에서 단어(예: '고민') 추출 시도
                string word = ExtractWordFromMorpheme(projectAssetsPath, setName, signFolderName);
                if (string.IsNullOrEmpty(word))
                {
                    // 모포임을 못 읽으면 폴더 이름 그대로 사용
                    word = signFolderName;
                }

                // 3-3. SignClip 에셋 생성/갱신
                string clipAssetPath = $"{CLIP_ASSET_FOLDER}/{signFolderName}.asset";
                var clip = AssetDatabase.LoadAssetAtPath<SignClip>(clipAssetPath);
                bool isNew = clip == null;
                if (isNew)
                {
                    clip = ScriptableObject.CreateInstance<SignClip>();
                    AssetDatabase.CreateAsset(clip, clipAssetPath);
                    Debug.Log($"[KSLDatabaseBuilder] 새 SignClip 생성: {clipAssetPath}");
                }

                clip.word = word;
                clip.keypointFrames = frames;

                EditorUtility.SetDirty(clip);
                createdClips.Add(clip);
            }
        }

        // 4. SignDatabase 생성/갱신
        var db = AssetDatabase.LoadAssetAtPath<SignDatabase>(DATABASE_ASSET_PATH);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<SignDatabase>();
            AssetDatabase.CreateAsset(db, DATABASE_ASSET_PATH);
            Debug.Log($"[KSLDatabaseBuilder] 새 SignDatabase 생성: {DATABASE_ASSET_PATH}");
        }

        db.clips = createdClips.ToArray();
        EditorUtility.SetDirty(db);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[KSLDatabaseBuilder] 완료: SignClip {createdClips.Count}개, SignDatabase 갱신됨.");
    }

    /// <summary>
    /// Clips / SignDataset 상위 폴더 생성 보조.
    /// </summary>
    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
        string name = Path.GetFileName(folderPath);

        if (string.IsNullOrEmpty(parent))
            parent = "Assets";

        if (!AssetDatabase.IsValidFolder(parent))
        {
            Debug.LogError($"[KSLDatabaseBuilder] 상위 폴더가 없습니다: {parent}");
            return;
        }

        AssetDatabase.CreateFolder(parent, name);
        Debug.Log($"[KSLDatabaseBuilder] 폴더 생성: {folderPath}");
    }

    /// <summary>
    /// 단어 폴더(예: NIA_SL_WORD0001_REAL17_F) 안의 *.json 들을
    /// 이름순으로 정렬해서 TextAsset 배열로 로드.
    /// </summary>
    private static TextAsset[] LoadKeypointFrames(string projectAssetsPath, string signDirAbs)
    {
        string[] jsonFiles = Directory.GetFiles(signDirAbs, "*.json");
        if (jsonFiles.Length == 0)
            return null;

        // 000000000.json, 000000001.json ... 이름 기준 정렬
        jsonFiles = jsonFiles
            .Select(p => p.Replace("\\", "/"))
            .OrderBy(p => p)
            .ToArray();

        var list = new List<TextAsset>();
        foreach (var absPath in jsonFiles)
        {
            // 절대경로 → Assets 시작 상대 경로
            string relPath = "Assets" + absPath.Substring(projectAssetsPath.Length);
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(relPath);
            if (ta != null)
                list.Add(ta);
            else
                Debug.LogWarning($"[KSLDatabaseBuilder] TextAsset 로드 실패: {relPath}");
        }

        return list.ToArray();
    }

    /// <summary>
    /// 모포임 JSON에서 수어 단어(예: '고민')를 추출.
    /// 파일 경로 규칙:
    /// Assets/SignDataset/Morpheme/{setName}/{signFolderName}_morpheme.json
    /// </summary>
    private static string ExtractWordFromMorpheme(string projectAssetsPath, string setName, string signFolderName)
    {
        string morAbsPath = Path.Combine(projectAssetsPath,
            $"SignDataset/Morpheme/{setName}/{signFolderName}_morpheme.json").Replace("\\", "/");

        if (!File.Exists(morAbsPath))
        {
            Debug.LogWarning($"[KSLDatabaseBuilder] 모포임 파일 없음: {morAbsPath}");
            return null;
        }

        string relPath = "Assets" + morAbsPath.Substring(projectAssetsPath.Length);
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(relPath);
        if (ta == null)
        {
            Debug.LogWarning($"[KSLDatabaseBuilder] 모포임 TextAsset 로드 실패: {relPath}");
            return null;
        }

        try
        {
            var root = JsonUtility.FromJson<KSLMorphemeRoot>(ta.text);
            if (root != null &&
                root.data != null && root.data.Length > 0 &&
                root.data[0].attributes != null && root.data[0].attributes.Length > 0)
            {
                string word = root.data[0].attributes[0].name;
                return word;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[KSLDatabaseBuilder] 모포임 파싱 실패: {relPath}\n{e}");
        }

        return null;
    }
}

/// <summary>
/// 모포임 JSON 파싱용 타입들
/// {
///   "metaData": { ... },
///   "data": [ { "start":..., "end":..., "attributes":[{"name":"고민"}] } ]
/// }
/// </summary>
[System.Serializable]
public class KSLMorphemeRoot
{
    public KSLMorphemeMeta metaData;
    public KSLMorphemeSegment[] data;
}

[System.Serializable]
public class KSLMorphemeMeta
{
    public string url;
    public string name;
    public float duration;
    public string exportedOn;
}

[System.Serializable]
public class KSLMorphemeSegment
{
    public float start;
    public float end;
    public KSLMorphemeAttribute[] attributes;
}

[System.Serializable]
public class KSLMorphemeAttribute
{
    public string name;
}