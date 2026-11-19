using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.IO;

public class GameManager : MonoBehaviour
{
    // =========================================================
    // 基本データ
    // =========================================================

    private string selected;
    private int score = -1000000;
    private int round = 1;
    private bool isShowingAnswerResult = false;

    private int[] valuesA = { 0, 0, 1, 1, 0, 1, 0, 0, 0, 0 };
    private int[] valuesB = { 1, 1, 0, 0, 1, 0, 1, 1, 1, 0 };

    private int[] selectedValues = new int[10];
    private string[] selectedChoices = new string[10];
    private int[] moneyAtChoice = new int[10];
    private int[] moneyAfterChoice = new int[10];

    // 問題文の個別上書き。
    // 空欄なら、計算方法・確率・報酬額から説明文を自動生成する。
    [SerializeField] private string[] customQuestionTextsA = new string[10];
    [SerializeField] private string[] customQuestionTextsB = new string[10];

    // =========================================================
    // 計算方法
    // =========================================================

    private enum ScoreCalculationMethod
    {
        AddReward,              // outcomeMoneyをそのまま加算
        AddCurrentScore,        // 現在の所持金×倍率を加算
        AddRewardEveryAnswer    // この問題では継続報酬だけを加算し、以降も継続
    }

    private int recurringBonusPerAnswer = 0;

    // Aを選んだときの計算方法
    private ScoreCalculationMethod[] scoreCalculationMethodsA =
    {
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddRewardEveryAnswer,
        ScoreCalculationMethod.AddCurrentScore,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward
    };

    // Bを選んだときの計算方法
    private ScoreCalculationMethod[] scoreCalculationMethodsB =
    {
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddRewardEveryAnswer,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward,
        ScoreCalculationMethod.AddReward
    };

    // =========================================================
    // 確率と獲得金額
    // 各行は問題番号、各列は結果1・結果2・結果3
    // =========================================================

    private int[,] probabilitiesA =
    {
        { 90, 10, 0 },
        { 80, 20, 0 },
        { 10, 20, 70 },
        { 1, 98, 1 },
        { 80, 20, 0 },
        { 40, 60, 0 },
        { 10, 90, 0 },
        { 50, 50, 0 },
        { 100, 0, 0 },
        { 50, 50, 0 }
    };

    private int[,] outcomeMoneyA =
    {
        { 50000, 0, 0 },
        { 20000, -20000, 0 },
        { -50, 100, 0 },
        { 10000000, 0, -10000000 },
        { 30000, 0, 0 },
        { 0, 0, 0 },
        { 0, 50000, 0 },
        { 10000, -50000, 0 },
        { -50000, 9, 0 },
        { 100000, 10, 0 }
    };

    private int[,] probabilitiesB =
    {
        { 45, 55, 0 },
        { 30, 70, 0 },
        { 70, 30, 0 },
        { 50, 50, 0 },
        { 40, 60, 0 },
        { 70, 30, 0 },
        { 55, 45, 0 },
        { 25, 75, 0 },
        { 50, 50, 0 },
        { 50, 50, 0 }
    };

    private int[,] outcomeMoneyB =
    {
        { 100000, -50000, 0 },
        { 50000, -50000, 0 },
        { 50000, 0, 0 },
        { 200000, 0, 0 },
        { 50000, 0, 0 },
        { 100000, 0, 0 },
        { 500000, 100000, 0 },
        { 300000, -150000, 0 },
        { -100000, 0, 0 },
        { 100000, 0, 0 }
    };

    // =========================================================
    // UI参照
    // =========================================================

    public Text leftButtonText;
    public Text rightButtonText;
    public Text answerResultText;
    public Text moneyText;
    public Text questionText;
    public Text gameEndText;
    public Text resultMoneyText;
    public Text resultTendencyText;

    public Image tendencyImage;
    public Text tendencyDescriptionText;
    public Sprite[] tendencyImages = new Sprite[5];

    public GameObject resultPanel;
    public Text resultDetailText;
    public GameObject detailButton;
    public Text detailButtonText;
    public Text questionTimerText;

    public GameObject titlePanel;
    public GameObject gameplayPanel;
    public GameObject moneyPanel;
    public GameObject questionTextObject;
    public GameObject leftButton;
    public GameObject rightButton;
    public GameObject answerResultPanel;

    // 回答後、MoneyPanelとAnswerResultPanel以外を暗くする半透明パネル
    public GameObject dimOverlay;

    // =========================================================
    // ステート管理
    // =========================================================

    private enum GameState
    {
        Title,
        Question,
        MoneyAnimating,
        Ending,
        AnswerResult,
        FinalResult,
        Detail
    }

    private GameState currentState = GameState.Title;

    // =========================================================
    // タイマー
    // =========================================================

    private Coroutine questionTimerCoroutine;
    private const float questionTimeLimit = 20f;

    // =========================================================
    // 金額アニメーション
    // =========================================================

    private int displayedScore;
    private Coroutine moneyAnimationCoroutine;
    [SerializeField] private float moneyAnimationDuration = 1f;

    private int pendingBeforeMoney;
    private int pendingGained;
    private int pendingAfterMoney;
    private bool pendingTimeout;

    // =========================================================
    // エンディング分岐
    // endingPanelsは金額が低い順に登録する
    // endingUpperLimitsは各エンディングの上限金額（以上ではなく以下）
    // 例：-1, 49999, 99999 の場合
    // 0番目：-1円以下
    // 1番目：0～49,999円
    // 2番目：50,000～99,999円
    // 最後：100,000円以上
    // =========================================================

    [SerializeField] private GameObject[] endingPanels = new GameObject[4];
    [SerializeField] private int[] endingUpperLimits = new int[3];

    // =========================================================
    // 初期化・更新
    // =========================================================

    private void Start()
    {
        currentState = GameState.Title;
        displayedScore = score;

        if (endingPanels != null)
        {
            foreach (GameObject panel in endingPanels)
            {
                if (panel != null)
                    panel.SetActive(false);
            }
        }

        if (answerResultPanel != null)
            answerResultPanel.SetActive(false);

        if (dimOverlay != null)
            dimOverlay.SetActive(false);

        UpdateMoneyText();
        UpdateQuestionText();
    }

    private void Update()
    {
        if (currentState != GameState.MoneyAnimating)
            return;

        bool tapped = Input.GetMouseButtonDown(0);

        if (Input.touchCount > 0 &&
            Input.GetTouch(0).phase == TouchPhase.Began)
        {
            tapped = true;
        }

        if (tapped)
            FinishMoneyAnimation();
    }

    private void UpdateMoneyText()
    {
        moneyText.text = "所持金：" + displayedScore.ToString("N0") + "円";
    }

    private void UpdateQuestionText()
    {
        // 1問目は残り10日、2問目は残り9日……10問目は残り1日
        int remainingDays = 11 - round;
        questionText.text = "残り" + remainingDays + "日";

        int questionIndex = round - 1;

        leftButtonText.text = GetChoiceDisplayText(
            questionIndex,
            scoreCalculationMethodsA[questionIndex],
            probabilitiesA,
            outcomeMoneyA,
            customQuestionTextsA
        );

        rightButtonText.text = GetChoiceDisplayText(
            questionIndex,
            scoreCalculationMethodsB[questionIndex],
            probabilitiesB,
            outcomeMoneyB,
            customQuestionTextsB
        );
    }

    // =========================================================
    // 選択肢の説明文を自動生成
    // =========================================================

    private string GetChoiceDisplayText(
        int questionIndex,
        ScoreCalculationMethod method,
        int[,] probabilities,
        int[,] rewards,
        string[] customTexts)
    {
        // 手動入力がある場合は、自動生成より優先する
        if (customTexts != null &&
            questionIndex >= 0 &&
            questionIndex < customTexts.Length &&
            !string.IsNullOrWhiteSpace(customTexts[questionIndex]))
        {
            return customTexts[questionIndex];
        }

        string displayText = "";

        for (int i = 0; i < 3; i++)
        {
            int probability = probabilities[questionIndex, i];
            int reward = rewards[questionIndex, i];

            // 確率0％、または報酬0の結果は表示しない
            if (probability <= 0 || reward == 0)
                continue;

            string effect;

            switch (method)
            {
                case ScoreCalculationMethod.AddReward:
                    effect = FormatSignedCompactAmount(reward);
                    break;

                case ScoreCalculationMethod.AddRewardEveryAnswer:
                    effect = "毎ターン" + FormatSignedCompactAmount(reward);
                    break;

                case ScoreCalculationMethod.AddCurrentScore:
                    // CalculateScore内の現在の整数除算と同じ倍率にする。
                    // 例：reward=-50なら1倍、reward=100なら2倍。
                    int multiplier = 1 + reward / 100;
                    effect = "所持金を" + multiplier + "倍";
                    break;

                default:
                    effect = FormatSignedCompactAmount(reward);
                    break;
            }

            if (displayText.Length > 0)
                displayText += "\n";

            displayText += probability + "％で" + effect;
        }

        return displayText.Length > 0 ? displayText : "変化なし";
    }

    // 例：50000 → +5万、-20000 → -2万
    private string FormatSignedCompactAmount(int amount)
    {
        string sign = amount > 0 ? "+" : "";
        long value = amount;

        if (value % 10000L == 0)
            return sign + (value / 10000L).ToString("N0") + "万";

        return sign + value.ToString("N0");
    }

    // =========================================================
    // タイマー処理
    // =========================================================

    private void StartQuestionTimer()
    {
        if (questionTimerCoroutine != null)
            StopCoroutine(questionTimerCoroutine);

        questionTimerCoroutine = StartCoroutine(QuestionTimer());
    }

    private void StopQuestionTimer()
    {
        if (questionTimerCoroutine != null)
        {
            StopCoroutine(questionTimerCoroutine);
            questionTimerCoroutine = null;
        }

        if (questionTimerText != null)
            questionTimerText.gameObject.SetActive(false);
    }

    private IEnumerator QuestionTimer()
    {
        float timeLeft = questionTimeLimit;

        questionTimerText.gameObject.SetActive(true);

        while (timeLeft > 0f)
        {
            questionTimerText.text =
                "残り " + Mathf.CeilToInt(timeLeft) + " 秒";

            yield return null;
            timeLeft -= Time.deltaTime;
        }

        questionTimerCoroutine = null;

        if (currentState != GameState.Question)
            yield break;

        moneyAtChoice[round - 1] = score;
        moneyAfterChoice[round - 1] = score;
        selectedChoices[round - 1] = "未選択";
        selectedValues[round - 1] = -1;

        BeginMoneyAnimation(score, 0, score, true);
    }

    // =========================================================
    // A・Bの選択
    // =========================================================

    public void SelectA()
    {
        if (currentState != GameState.Question)
            return;

        StopQuestionTimer();

        selected = "A";

        int beforeMoney = score;

        moneyAtChoice[round - 1] = beforeMoney;
        selectedChoices[round - 1] = "A";
        selectedValues[round - 1] = valuesA[round - 1];

        int outcome = RollThreeProbabilities(
            probabilitiesA[round - 1, 0],
            probabilitiesA[round - 1, 1],
            probabilitiesA[round - 1, 2]
        );

        if (outcome == 0)
        {
            StartQuestionTimer();
            return;
        }

        int reward = outcomeMoneyA[round - 1, outcome - 1];

        int afterScore = CalculateScore(
            score,
            reward,
            scoreCalculationMethodsA[round - 1]
        );

        int gained = afterScore - score;
        score = afterScore;
        moneyAfterChoice[round - 1] = score;

        BeginMoneyAnimation(beforeMoney, gained, score);
        Debug.Log(score);
    }

    public void SelectB()
    {
        if (currentState != GameState.Question)
            return;

        StopQuestionTimer();

        selected = "B";

        int beforeMoney = score;

        moneyAtChoice[round - 1] = beforeMoney;
        selectedChoices[round - 1] = "B";
        selectedValues[round - 1] = valuesB[round - 1];

        int outcome = RollThreeProbabilities(
            probabilitiesB[round - 1, 0],
            probabilitiesB[round - 1, 1],
            probabilitiesB[round - 1, 2]
        );

        if (outcome == 0)
        {
            StartQuestionTimer();
            return;
        }

        int reward = outcomeMoneyB[round - 1, outcome - 1];

        int afterScore = CalculateScore(
            score,
            reward,
            scoreCalculationMethodsB[round - 1]
        );

        int gained = afterScore - score;
        score = afterScore;
        moneyAfterChoice[round - 1] = score;

        BeginMoneyAnimation(beforeMoney, gained, score);
        Debug.Log(score);
    }

    // =========================================================
    // 金額アニメーション
    // =========================================================

    private void BeginMoneyAnimation(
        int beforeMoney,
        int gained,
        int afterMoney,
        bool timeout = false)
    {
        pendingBeforeMoney = beforeMoney;
        pendingGained = gained;
        pendingAfterMoney = afterMoney;
        pendingTimeout = timeout;

        currentState = GameState.MoneyAnimating;
        isShowingAnswerResult = true;

        displayedScore = beforeMoney;
        UpdateMoneyText();

        if (timeout ||
            beforeMoney == afterMoney ||
            moneyAnimationDuration <= 0f)
        {
            FinishMoneyAnimation();
            return;
        }

        if (moneyAnimationCoroutine != null)
            StopCoroutine(moneyAnimationCoroutine);

        moneyAnimationCoroutine = StartCoroutine(AnimateMoney());
    }

    private IEnumerator AnimateMoney()
    {
        float elapsed = 0f;

        while (elapsed < moneyAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / moneyAnimationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            displayedScore = Mathf.RoundToInt(
                Mathf.Lerp(pendingBeforeMoney, pendingAfterMoney, eased)
            );

            UpdateMoneyText();
            yield return null;
        }

        moneyAnimationCoroutine = null;
        FinishMoneyAnimation();
    }

    private void FinishMoneyAnimation()
    {
        if (currentState != GameState.MoneyAnimating)
            return;

        if (moneyAnimationCoroutine != null)
        {
            StopCoroutine(moneyAnimationCoroutine);
            moneyAnimationCoroutine = null;
        }

        displayedScore = pendingAfterMoney;
        UpdateMoneyText();

        // 金額アニメーション終了後は、必ず回答結果画面へ進む。
        // エンディング判定は10問目の回答結果画面をタップした後に行う。
        StartCoroutine(
            ShowAnswerResult(
                pendingBeforeMoney,
                pendingGained,
                pendingAfterMoney,
                pendingTimeout
            )
        );
    }

    // =========================================================
    // クリック・タップ入力待ち
    // 押してから離したときに終了する
    // =========================================================

    private IEnumerator WaitForClickRelease()
    {
        // 直前の入力が離されるまで待つ
        while (Input.GetMouseButton(0) || Input.touchCount > 0)
            yield return null;

        bool mousePressed = false;
        bool touchPressed = false;

        while (true)
        {
            if (Input.GetMouseButtonDown(0))
                mousePressed = true;

            if (mousePressed && Input.GetMouseButtonUp(0))
                yield break;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                    touchPressed = true;

                if (touchPressed &&
                    (touch.phase == TouchPhase.Ended ||
                     touch.phase == TouchPhase.Canceled))
                {
                    yield break;
                }
            }

            yield return null;
        }
    }

    // =========================================================
    // 選択結果画面
    // =========================================================

    private IEnumerator ShowAnswerResult(
        int beforeMoney,
        int gained,
        int afterMoney,
        bool timeout = false)
    {
        currentState = GameState.AnswerResult;
        isShowingAnswerResult = true;


        string gainedText = (gained >= 0 ? "+" : "") + gained.ToString("N0");

        if (timeout)
        {
            answerResultText.text =
                "あなたは選択しなかった\n\n" +
                "画面をタップして次へ";
        }
        else
        {
            answerResultText.text =
                "回答前の金額：" + beforeMoney.ToString("N0") + "円\n" +
                "獲得金額：" + gainedText + "円\n" +
                "回答後の金額：" + afterMoney.ToString("N0") + "円\n\n" +
                "画面をタップして次へ";
        }

        // AnswerResultPanelが表示される瞬間に暗幕を表示する
        if (dimOverlay != null)
            dimOverlay.SetActive(true);

        answerResultPanel.SetActive(true);
        answerResultText.gameObject.SetActive(true);

        if (round == 10)
            SaveDetails();

        yield return WaitForClickRelease();

        // AnswerResultPanelがタップされたので暗幕を解除する
        if (dimOverlay != null)
            dimOverlay.SetActive(false);

        answerResultPanel.SetActive(false);

        if (round < 10)
        {
            round++;
            UpdateQuestionText();

            leftButton.SetActive(true);
            rightButton.SetActive(true);

            currentState = GameState.Question;
            isShowingAnswerResult = false;

            StartQuestionTimer();
        }
        else
        {
            isShowingAnswerResult = false;

            // 10問目の回答結果画面をタップした後、最終金額でエンディングを選ぶ
            GameObject selectedEndingPanel = GetEndingPanel(score);

            if (selectedEndingPanel != null)
            {
                currentState = GameState.Ending;
                selectedEndingPanel.SetActive(true);

                yield return WaitForClickRelease();

                selectedEndingPanel.SetActive(false);
            }

            currentState = GameState.FinalResult;
            HideGameUI();
            StartCoroutine(ShowResult());
        }
    }

    // =========================================================
    // 最終金額に対応するエンディングを取得
    // =========================================================

    private GameObject GetEndingPanel(int finalScore)
    {
        if (endingPanels == null || endingPanels.Length == 0)
        {
            Debug.LogWarning("EndingPanelsがInspectorに設定されていません。");
            return null;
        }

        if (endingUpperLimits == null ||
            endingUpperLimits.Length != endingPanels.Length - 1)
        {
            Debug.LogError(
                "EndingUpperLimitsの要素数は、EndingPanelsの要素数より1少なくしてください。"
            );
            return null;
        }

        // 上限金額を低い順に調べ、最初に当てはまるエンディングを使う
        for (int i = 0; i < endingUpperLimits.Length; i++)
        {
            if (finalScore <= endingUpperLimits[i])
            {
                if (endingPanels[i] == null)
                    Debug.LogError("EndingPanelsのElement " + i + " が未設定です。");

                return endingPanels[i];
            }
        }

        // すべての上限を超えた場合は最後のエンディング
        int lastIndex = endingPanels.Length - 1;

        if (endingPanels[lastIndex] == null)
            Debug.LogError("EndingPanelsのElement " + lastIndex + " が未設定です。");

        return endingPanels[lastIndex];
    }

    // =========================================================
    // 選択傾向
    // =========================================================

    private void CheckTendency()
    {
        int safeCount = 0;
        int riskCount = 0;

        for (int i = 0; i < selectedValues.Length; i++)
        {
            if (selectedValues[i] == 0)
                safeCount++;
            else if (selectedValues[i] == 1)
                riskCount++;
        }

        int tendencyIndex;
        string tendencyName;
        string description;

        if (safeCount == 10)
        {
            tendencyIndex = 0;
            tendencyName = "チキン";
            description = "慎重な選択を大切にするタイプです。";
        }
        else if (safeCount > 5)
        {
            tendencyIndex = 1;
            tendencyName = "安全志向";
            description = "リスクを避け、安定を重視するタイプです。";
        }
        else if (riskCount == 9)
        {
            tendencyIndex = 2;
            tendencyName = "バカ";
            description = "大胆な選択を好むタイプです。";
        }
        else if (riskCount > 5)
        {
            tendencyIndex = 3;
            tendencyName = "リスク志向";
            description = "大きな結果を狙って挑戦するタイプです。";
        }
        else
        {
            tendencyIndex = 4;
            tendencyName = "バランス型";
            description = "安全とリスクを使い分けるタイプです。";
        }

        resultTendencyText.text = "選択傾向：" + tendencyName;

        if (tendencyImages != null &&
            tendencyImages.Length > tendencyIndex &&
            tendencyImages[tendencyIndex] != null)
        {
            tendencyImage.sprite = tendencyImages[tendencyIndex];
            tendencyImage.enabled = true;
        }
        else
        {
            tendencyImage.enabled = false;
            Debug.LogWarning(tendencyName + "の画像が設定されていません。");
        }

        tendencyDescriptionText.text = description;
    }

    // =========================================================
    // 最終結果
    // =========================================================

    private IEnumerator ShowResult()
    {
        currentState = GameState.FinalResult;

        yield return new WaitForSeconds(1f);

        resultMoneyText.text =
            "最終所持金：" + score.ToString("N0") + "p";
        resultMoneyText.gameObject.SetActive(true);

        yield return new WaitForSeconds(1f);

        CheckTendency();
        resultTendencyText.gameObject.SetActive(true);

        detailButton.SetActive(true);
    }

    // =========================================================
    // 詳細画面
    // =========================================================

    public void ShowDetails()
    {
        currentState = GameState.Detail;

        resultMoneyText.gameObject.SetActive(false);
        resultTendencyText.gameObject.SetActive(false);

        string detail = "";

        for (int i = 0; i < 10; i++)
        {
            string tendency;

            if (selectedValues[i] == 0)
                tendency = "安全";
            else if (selectedValues[i] == 1)
                tendency = "リスク";
            else
                tendency = "未選択";

            string beforeMoney =
                moneyAtChoice[i].ToString("N0").PadLeft(11) + "p";

            string afterMoney =
                moneyAfterChoice[i].ToString("N0").PadLeft(11) + "p";

            string choice;

            if (selectedChoices[i] == "A")
                choice = "A     ";
            else if (selectedChoices[i] == "B")
                choice = "B     ";
            else
                choice = "未選択";

            detail +=
                (i + 1).ToString().PadLeft(2) + "問目　" +
                beforeMoney + " → " +
                afterMoney + "　" +
                choice + "　" +
                tendency + "\n";
        }

        resultDetailText.text = detail;
        resultDetailText.gameObject.SetActive(true);
        detailButtonText.text = "戻る";
    }

    public void HideDetail()
    {
        currentState = GameState.FinalResult;

        resultDetailText.gameObject.SetActive(false);
        resultMoneyText.gameObject.SetActive(true);
        resultTendencyText.gameObject.SetActive(true);

        detailButtonText.text = "詳細";
    }

    public void DetailButton()
    {
        if (resultDetailText.gameObject.activeSelf)
            HideDetail();
        else
            ShowDetails();
    }

    // =========================================================
    // ゲーム開始・画面切り替え
    // =========================================================

    public void StartGame()
    {
        currentState = GameState.Question;
        isShowingAnswerResult = false;

        displayedScore = score;
        UpdateMoneyText();

        titlePanel.SetActive(false);
        gameplayPanel.SetActive(true);

        if (dimOverlay != null)
            dimOverlay.SetActive(false);

        moneyPanel.SetActive(true);
        questionTextObject.SetActive(true);
        leftButton.SetActive(true);
        rightButton.SetActive(true);

        StartQuestionTimer();
    }

    private void HideGameUI()
    {
        gameplayPanel.SetActive(false);
        resultPanel.SetActive(true);
    }

    // =========================================================
    // 結果保存
    // =========================================================

    public void SaveDetails()
    {
        string detail = "";

        for (int i = 0; i < 10; i++)
        {
            string tendency;

            if (selectedValues[i] == 0)
                tendency = "安全";
            else if (selectedValues[i] == 1)
                tendency = "リスク";
            else
                tendency = "未選択";

            detail +=
                (i + 1).ToString().PadLeft(2) + "問目  " +
                moneyAtChoice[i].ToString("N0").PadLeft(12) + "円  →  " +
                moneyAfterChoice[i].ToString("N0").PadLeft(12) + "円  " +
                selectedChoices[i] + "  " +
                tendency + "\n";
        }

        string path = Path.Combine(
            Application.persistentDataPath,
            "GameResult.txt"
        );

        File.AppendAllText(
            path,
            "\n\n===== ゲーム結果 " +
            System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") +
            " =====\n" +
            detail
        );

        Debug.Log(path);
    }

    // =========================================================
    // 3種類の確率抽選
    // =========================================================

    private int RollThreeProbabilities(int p1, int p2, int p3)
    {
        if (p1 < 0 || p2 < 0 || p3 < 0 ||
            p1 + p2 + p3 != 100)
        {
            Debug.LogError("3つの確率の合計を100%にしてください。");
            return 0;
        }

        int random = Random.Range(1, 101);

        if (random <= p1)
            return 1;
        else if (random <= p1 + p2)
            return 2;
        else
            return 3;
    }

    // =========================================================
    // 所持金の計算
    // =========================================================

    private int CalculateScore(
        int currentScore,
        int reward,
        ScoreCalculationMethod method)
    {
        // この問題の回答時に継続報酬へ登録
        if (method == ScoreCalculationMethod.AddRewardEveryAnswer)
            recurringBonusPerAnswer += reward;

        switch (method)
        {
            case ScoreCalculationMethod.AddReward:
                return currentScore + reward + recurringBonusPerAnswer;

            case ScoreCalculationMethod.AddCurrentScore:
                return currentScore
                    + currentScore * (reward / 100)
                    + recurringBonusPerAnswer;

            case ScoreCalculationMethod.AddRewardEveryAnswer:
                // 登録した問題では継続報酬だけを加算
                return currentScore + recurringBonusPerAnswer;

            default:
                return currentScore + reward + recurringBonusPerAnswer;
        }
    }
}
