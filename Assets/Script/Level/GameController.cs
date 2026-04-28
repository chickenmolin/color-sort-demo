using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Threading;

// 1. Công dụng file:
// - Quản lý logic chính của game Water Sort
// - Xử lý input click chọn chai
// - Điều khiển đổ màu giữa các bottle
// - Quản lý level (load, next, previous)
// - Kiểm tra điều kiện thắng và hiển thị UI

// 2. Các mục quan trọng:

// a) Dữ liệu chính:
// - FirstBottle: chai được chọn đầu tiên
// - SecondBottle: chai được chọn thứ hai
// - bottles: danh sách tất cả chai trong level
// - allFull: trạng thái tất cả chai đã hoàn thành
// - LevelCompleted: UI khi thắng

// b) Level system:
// - currentLevel: level hiện tại
// - levelToUnlock: level sẽ được mở khóa
// - numberOfUnlockedLevel: số level đã unlock
// - LevelLoader: load dữ liệu level

// c) Input & tương tác:
// - Click chuột để chọn chai (Raycast2D)
// - Click lại để bỏ chọn
// - Click chai khác để thực hiện đổ màu

// d) Animation logic:
// - bottleUp: nâng chai lên khi chọn
// - bottleDown: hạ chai xuống khi bỏ chọn

// e) Logic gameplay:
// - Kiểm tra có thể đổ màu hay không
// - Gọi hàm transfer màu giữa 2 chai
// - Reset trạng thái sau mỗi lần thao tác

// f) Win condition:
// - Tất cả chai rỗng hoặc full cùng 1 màu
// - Delay 1s rồi gọi Win()
// - Lưu progress bằng PlayerPrefs

public class GameController : MonoBehaviour
{

    public BottleController FirstBottle;   // Chai nguồn (được chọn trước)
    public BottleController SecondBottle;  // Chai đích (được chọn sau)

    public BottleController[] bottles;     // Toàn bộ chai trong màn chơi

    private bool allFull = false;          // True = tất cả chai đã đầy → win
    public int levelToUnlock;              // Màn sẽ được mở khóa khi thắng
    int numberOfUnlockedLevel;             // Số màn đã mở khóa hiện tại

    public GameObject LevelCompleted;      // UI hiện ra khi hoàn thành màn

    private float bottleUp = 0.3f;        // Độ dịch lên khi chọn chai
    private float bottleDown = -0.3f;     // Độ dịch xuống khi bỏ chọn chai

    private LevelLoader levelLoader;       // Quản lý chuyển màn
    private int currentLevel = 1;         // Màn hiện tại

    private void Start()
    {
        levelLoader = GetComponent<LevelLoader>();
        LoadCurrentLevel();
    }

    // Tải màn hiện tại
    public void LoadCurrentLevel() { levelLoader.RenderLevel(currentLevel); }
    
    // Chuyển màn tiếp theo, nếu hết thì quay về màn 1
    public void NextLevel()
    {
        currentLevel++;
        if (currentLevel > levelLoader.GetTotalLevels()) currentLevel = 1;
        LoadCurrentLevel();
    }
    
    // Quay lại màn trước, nếu đang màn 1 thì về màn cuối
    public void PreviousLevel()
    {
        currentLevel--;
        if (currentLevel < 1) currentLevel = levelLoader.GetTotalLevels();
        LoadCurrentLevel();
    }
    
    void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            // Raycast từ vị trí click chuột vào scene
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(mousePos.x, mousePos.y), Vector2.zero);
    
            if(hit.collider != null && hit.collider.GetComponent<BottleController>() != null)
            {
                if(FirstBottle == null)
                {
                    // Chọn chai đầu tiên → nâng lên
                    FirstBottle = hit.collider.GetComponent<BottleController>();
                    if(FirstBottle.numberOfColorsInBottle != 0)
                        FirstBottle.transform.position += new Vector3(0, bottleUp, 0);
                }
                else
                {
                    if(FirstBottle == hit.collider.GetComponent<BottleController>())
                    {
                        // Click lại chai cũ → bỏ chọn, hạ xuống
                        if(FirstBottle.numberOfColorsInBottle != 0)
                            FirstBottle.transform.position += new Vector3(0, bottleDown, 0);
                        FirstBottle = null;
                    }
                    else
                    {
                        // Chọn chai thứ 2 → kiểm tra có đổ được không
                        SecondBottle = hit.collider.GetComponent<BottleController>();
                        FirstBottle.bottleControllerRef = SecondBottle;
                        FirstBottle.UpdateTopColorValue();
                        SecondBottle.UpdateTopColorValue();
    
                        if(SecondBottle.FillBottleCheck(FirstBottle.topColor))
                            FirstBottle.startColorTransfer(); // Hợp lệ → bắt đầu đổ
                        else
                            FirstBottle.transform.position += new Vector3(0, bottleDown, 0); // Không hợp lệ → hạ xuống
    
                        FirstBottle = null;
                        SecondBottle = null;
                    }
                }
            }
            else
            {
                // Click ra ngoài → bỏ chọn chai
                if(FirstBottle.numberOfColorsInBottle != 0)
                    FirstBottle.transform.position += new Vector3(0, bottleDown, 0);
                FirstBottle = null;
                SecondBottle = null;
            }
        }
    
        // Mỗi frame kiểm tra điều kiện thắng
        if(!allFull) StartCoroutine(AllBottlesAreFull());
    }
    
    // Thắng khi mọi chai: rỗng hoặc đủ 4 lớp cùng màu
    IEnumerator AllBottlesAreFull()
    {
        if(bottles.All(y => y.numberOfColorsInBottle == 0 || y.numberOfTopColorLayer == 4))
        {
            allFull = true;
            yield return new WaitForSeconds(1f);
            Win();
        }
    }
    
    // Lưu tiến độ và hiện UI chiến thắng
    private void Win()
    {
        if(!allFull) return;
    
        numberOfUnlockedLevel = PlayerPrefs.GetInt("LevelIsUnlocked");
        if(numberOfUnlockedLevel <= levelToUnlock)
            PlayerPrefs.SetInt("LevelIsUnlocked", numberOfUnlockedLevel + 1); // Mở khóa màn tiếp
    
        if(!LevelCompleted.activeSelf)
            LevelCompleted.SetActive(true); // Hiện màn hình chiến thắng
    }


}
