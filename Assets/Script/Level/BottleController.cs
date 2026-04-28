using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

// 1. Công dụng file:
// - Điều khiển logic chai nước trong game (kiểu Water Sort)
// - Xử lý đổ màu giữa các chai
// - Quản lý animation: di chuyển, xoay, hiệu ứng nước
// - Cập nhật hiển thị nước bằng shader

// 2. Các mục quan trọng:

// a) Dữ liệu chính:
// - bottleColors: mảng chứa màu trong chai (tối đa 4)
// - numberOfColorsInBottle: số lớp màu hiện tại
// - topColor: màu trên cùng
// - numberOfTopColorLayer: số lớp cùng màu ở trên cùng
// - bottleControllerRef: tham chiếu chai đích

// b) Hiển thị & animation:
// - bottleMaskSR: SpriteRenderer dùng shader hiển thị nước
// - fillAmounts: mức fill tương ứng số màu
// - rotationValues: góc xoay khi rót
// - AnimationCurve: điều khiển tốc độ, xoay, fill
// - lineRenderer: hiệu ứng dòng nước khi rót

// c) Hàm khởi tạo:
// - Start(): set vị trí ban đầu, fill, màu, topColor

// d) Logic chính:
// - Update():
//   + Kiểm tra click chuột
//   + Kiểm tra có thể rót (FillBottleCheck)
//   + Tính số lượng màu cần chuyển
//   + Copy màu sang chai đích
//   + Bắt đầu animation (MoveBottle)

// e) Animation:
// - MoveBottle(): di chuyển chai đến vị trí rót
// - RotateBottle(): xoay chai + đổ nước (logic chính)
// - RotateBottlrBack(): xoay lại vị trí ban đầu
// - MoveBottleBack(): trả chai về chỗ cũ

// f) Xử lý màu:
// - UpdateTopColorValue(): xác định màu trên cùng + số layer giống nhau
// - UpdateColorsOnShader(): cập nhật màu lên shader

// g) Điều kiện rót:
// - FillBottleCheck(): kiểm tra có rót được không
//   + Chai rỗng → OK
//   + Cùng màu → OK
//   + Đầy hoặc khác màu → không cho

// h) Tính toán:
// - CalculateRotationIndex(): tính góc xoay phù hợp
// - FillUp(): tăng lượng nước chai đích

// i) Điều khiển tương tác:
// - LockAll(): khóa tất cả chai khi đang rót
// - UnlockAll(): mở khóa sau khi xong
// - LockBottle(): khóa chai nếu đã full cùng màu

// j) Hiệu ứng:
// - PlayBoilingSound(): âm thanh khi rót
// - lineRenderer: vẽ dòng nước

// k) Fix lỗi:
// - FixAmount(): chỉnh lại lượng nước cho chính xác (tránh sai số float)

// l) Hỗ trợ:
// - chosenRotationPointAndDirection(): chọn hướng xoay trái/phải

public class BottleController : MonoBehaviour
{
// Mảng chứa màu trong chai (tối đa 4 lớp) 
    [SerializeField] Color[] bottleColors; 
    // Sprite dùng shader để hiển thị nước 
    [SerializeField] SpriteRenderer bottleMaskSR; 
    // Curve để điều khiển animation (scale + xoay + tốc độ) 
    [SerializeField] AnimationCurve ScaleAndRotationMutiplaierCurve; 
    [SerializeField] AnimationCurve FillAmountCurve; 
    [SerializeField] AnimationCurve RotaationSpeedMultiplaier; 
    
    // Giá trị fill tương ứng với số lượng màu 
    [SerializeField] float[] fillAmounts; 
    // Giá trị góc xoay tương ứng 
    [SerializeField] float[] rotationValues;
    private int rotationIndex; // index để lấy góc xoay phù hợp 
    // Số lượng màu hiện tại trong chai (0 → 4) 
    [Range(0,4)] public int numberOfColorsInBottle = 4; 
    // Màu trên cùng của chai 
    public Color topColor; 
    // Số lớp liên tiếp cùng màu ở trên cùng 
    public int numberOfTopColorLayer = 0; 
    // Tham chiếu đến chai sẽ được rót vào 
    public BottleController bottleControllerRef; 
    // Số màu/layer sẽ được chuyển 
    private int numberOfColorsToTranfer = 0; 
    private int numberOfLayersToTranfer = 0; 
    // 2 điểm xoay (trái/phải) để tạo hiệu ứng rót 
    [SerializeField] Transform leftRotationPoint; 
    [SerializeField] Transform rightRotationPoint;     
    private Transform chosenRotationPoint; 
    private float directionMultiplaier = 1.0f; // hướng xoay

    // Dùng cho animation di chuyển
    Vector3 startPosition;
    Vector3 endPosition;
    Vector3 originalPosition;

    public LineRenderer lineRenderer;

    [SerializeField] float timeToRotate = 1.0f; // thời gian xoay


    GameController myObj1 = new GameController();
 
    public AudioSource boilingSound;

    private GameObject[] levelbottles;
    private GameObject levelbottle;

    private float addedAmount;


    void Start()
    {
        // Lưu vị trí ban đầu 
        originalPosition = transform.position; 
        // Set mức fill ban đầu theo số màu 
        bottleMaskSR.material.SetFloat("_FillAmount", fillAmounts[numberOfColorsInBottle]); 
        // Update màu lên shader 
        UpdateColorsOnShader(); 
        // Xác định màu trên cùng 
        UpdateTopColorValue();
    }

    void Update()
    {
        // Giới hạn số màu từ 0 → 4 (tránh lỗi)
        numberOfColorsInBottle = numberOfColorsInBottle > 4? 4 : numberOfColorsInBottle;
        numberOfColorsInBottle = numberOfColorsInBottle < 0? 0 : numberOfColorsInBottle;

        if(Input.GetMouseButtonDown(0) && myObj1.FirstBottle != null )  
        {
            UpdateTopColorValue();

            if(bottleControllerRef.FillBottleCheck(topColor))
            {
                // Xác định điểm xoay + hướng
                chosenRotationPointAndDirection();
                // Tính số lượng màu có thể chuyển
                numberOfColorsToTranfer = Mathf.Min(numberOfTopColorLayer, 4 - bottleControllerRef.numberOfColorsInBottle);
                numberOfLayersToTranfer = Mathf.Min(numberOfTopColorLayer, 4 - bottleControllerRef.numberOfColorsInBottle);

                // Copy màu sang chai đích
                for(int i = 0 ; i < numberOfColorsToTranfer ; i++)
                {
                    bottleControllerRef.bottleColors[bottleControllerRef.numberOfColorsInBottle + i ] = topColor;
                }

                bottleControllerRef.UpdateColorsOnShader();
            }
            // else
            // {
            //     myObj1.FirstBottle = null;
            //     myObj1.SecondBottle = null;
            // }

            // Tính góc xoay phù hợp
            CalculateRotationIndex(4 - bottleControllerRef.numberOfColorsInBottle);

            
             StartCoroutine(MoveBottle()); 

        }

    }

    IEnumerator MoveBottle()
    {

        startPosition = transform.position;
        // Xác định vị trí đích (trái/phải)
        if(chosenRotationPoint == leftRotationPoint)
        {
            endPosition = bottleControllerRef.rightRotationPoint.position;

        }
        else
        {
            endPosition = bottleControllerRef.leftRotationPoint.position;
        }

        float t1 = 0;

        while(t1 <= 1)
        {
            transform.position = Vector3.Lerp(startPosition, endPosition, t1);

            t1 += Time.deltaTime * 2;

            yield return new WaitForEndOfFrame();
        }

        transform.position = endPosition;

        // Sau khi di chuyển xong → xoay chai
         StartCoroutine(RotateBottle());
    }

    IEnumerator MoveBottleBack()
    {


        startPosition = transform.position;
        endPosition = originalPosition;


        float t2 = 0;

        while(t2 <= 1)
        {
            transform.position = Vector3.Lerp(startPosition, endPosition, t2);
            t2 += Time.deltaTime * 2;

            yield return new WaitForEndOfFrame();
        }

        transform.position = endPosition;

        transform.GetComponent<SpriteRenderer>().sortingOrder -= 2;
        bottleMaskSR.sortingOrder -= 2;
      
        UnlockAll();
        StartCoroutine( LockBottle() );  // neu ong full thi dong

    }
    // Hàm bắt đầu quá trình rót màu từ chai này sang chai khác
    public void startColorTransfer()
    {
        LockAll();// Khóa toàn bộ chai (tránh user click nhiều lần khi animation đang chạy)

        // Xác định điểm xoay (trái/phải) và hướng xoay
        chosenRotationPointAndDirection();

        // Tính số lượng màu có thể chuyển: 
        // - Không vượt quá số layer cùng màu trên cùng 
        // - Không vượt quá dung lượng còn trống của chai đích
        numberOfColorsToTranfer = Mathf.Min(numberOfTopColorLayer, 4 - bottleControllerRef.numberOfColorsInBottle);
        numberOfLayersToTranfer = Mathf.Min(numberOfTopColorLayer, 4 - bottleControllerRef.numberOfColorsInBottle);

        for(int i = 0 ; i < numberOfColorsToTranfer ; i++)
        {
            bottleControllerRef.bottleColors[bottleControllerRef.numberOfColorsInBottle + i ] = topColor;

        }

        bottleControllerRef.UpdateColorsOnShader();
        
        // Tính toán góc xoay phù hợp dựa trên số lượng sẽ rót
        CalculateRotationIndex(4 - bottleControllerRef.numberOfColorsInBottle);

        transform.GetComponent<SpriteRenderer>().sortingOrder += 2;
        bottleMaskSR.sortingOrder += 2;

          StartCoroutine(MoveBottle()); 
    }

    private void UpdateColorsOnShader()
    {
        bottleMaskSR.material.SetColor("_Color01", bottleColors[0]);
        bottleMaskSR.material.SetColor("_Color02", bottleColors[1]);
        bottleMaskSR.material.SetColor("_Color03", bottleColors[2]);
        bottleMaskSR.material.SetColor("_Color04", bottleColors[3]);
    }

    // Coroutine xử lý animation xoay chai + rót nước
    IEnumerator RotateBottle() 
    {
        float t = 0f;
        float lerpValue;
        float angleVlaue; // góc xoay hiện tại

        float lastAngleValue  = 0f;// lưu góc frame trước để tính delta

        while(t < timeToRotate)
        {
            lerpValue = t / timeToRotate; // Chuẩn hóa thời gian về 0 → 1
            // góc xoay từ 0 → góc tối đa
            angleVlaue = Mathf.Lerp(0.0f, directionMultiplaier * rotationValues[rotationIndex], lerpValue);
            // Xoay chai quanh điểm
            transform.RotateAround(chosenRotationPoint.position, Vector3.forward, lastAngleValue - angleVlaue);
            // Update hiệu ứng scale + rotation trong shader
            bottleMaskSR.material.SetFloat("_ScaleAndRotationMultiplaier",
                                             ScaleAndRotationMutiplaierCurve.Evaluate(angleVlaue));
           
            if(fillAmounts[numberOfColorsInBottle] > FillAmountCurve.Evaluate(angleVlaue)  ) //+ 0.005
            {

                if(lineRenderer.enabled == false)
                {
                    PlayBoilingSound();

                    lineRenderer.startColor = topColor;
                    lineRenderer.endColor = topColor;

                    lineRenderer.SetPosition(0, chosenRotationPoint.position);
                    lineRenderer.SetPosition(1, chosenRotationPoint.position - Vector3.up * 1.45f);  

                    lineRenderer.enabled = true;
                }

                // Giảm lượng nước trong chai nguồn
                bottleMaskSR.material.SetFloat("_FillAmount", FillAmountCurve.Evaluate(angleVlaue)); // First bottle
                // Tính lượng nước đã đổ trong frame này
                addedAmount = FillAmountCurve.Evaluate(lastAngleValue) - FillAmountCurve.Evaluate(angleVlaue) ;
                // Tăng lượng nước cho chai đích
                bottleControllerRef.FillUp(addedAmount);
            }

            t +=  Time.deltaTime * RotaationSpeedMultiplaier.Evaluate(angleVlaue);

            lastAngleValue = angleVlaue;

            yield return new WaitForEndOfFrame();
        }

        angleVlaue = directionMultiplaier * rotationValues[rotationIndex];

        bottleMaskSR.material.SetFloat("_ScaleAndRotationMultiplaier",
                                         ScaleAndRotationMutiplaierCurve.Evaluate(angleVlaue));
        bottleMaskSR.material.SetFloat("_FillAmount", FillAmountCurve.Evaluate(angleVlaue));

        // Cập nhật lại số lượng màu thực tế sau khi rót xong
        numberOfColorsInBottle -= numberOfColorsToTranfer;

        bottleControllerRef.numberOfColorsInBottle += numberOfColorsToTranfer;
        bottleControllerRef.numberOfTopColorLayer += numberOfLayersToTranfer;
        
        lineRenderer.enabled = false;
        boilingSound.Stop();


        StartCoroutine(RotateBottlrBack());
    }

    IEnumerator RotateBottlrBack()
    {
        float t = 0f;
        float lerpValue;
        float angleVlaue;

        float lastAngleValue = directionMultiplaier * rotationValues[rotationIndex];


        while(t < timeToRotate) // Vòng lặp trong suốt thời gian quay
        {
            StartCoroutine(FixAmount()); // Sửa lượng chất lỏng trong chai mỗi frame
            lerpValue = t / timeToRotate; 
            
            // suy góc từ góc hiện tại về 0 (quay ngược về vị trí gốc)
            angleVlaue = Mathf.Lerp(directionMultiplaier * rotationValues[rotationIndex], 0f, lerpValue);
            // Xoay chai quanh điểm cố định
            transform.RotateAround(chosenRotationPoint.position, Vector3.forward, lastAngleValue - angleVlaue);
            // Cập nhật shader hiệu ứng chất lỏng
            bottleMaskSR.material.SetFloat("_ScaleAndRotationMultiplaier",
                                             ScaleAndRotationMutiplaierCurve.Evaluate(angleVlaue));
 
            lastAngleValue = angleVlaue;// Lưu góc frame này cho frame tiếp theo

            t+=  Time.deltaTime ;

            yield return new WaitForEndOfFrame();
        }

        UpdateTopColorValue();

        angleVlaue = 0;
        transform.eulerAngles = new Vector3(0, 0, angleVlaue);
        bottleMaskSR.material.SetFloat("_ScaleAndRotationMultiplaier",
                                         ScaleAndRotationMutiplaierCurve.Evaluate(angleVlaue));

        StartCoroutine(MoveBottleBack());
    }

    public int  UpdateTopColorValue()
    {
        if(numberOfColorsInBottle != 0)
        {
            numberOfTopColorLayer = 1;

            topColor = bottleColors[numberOfColorsInBottle - 1];

            if(numberOfColorsInBottle == 4)
            {

                 if(bottleColors[3].Equals(bottleColors[2]))
                 {
                    numberOfTopColorLayer = 2;

                    if(bottleColors[2].Equals(bottleColors[1]))
                    {

                        numberOfTopColorLayer = 3;

                        if(bottleColors[1].Equals(bottleColors[0]))
                        {
                            numberOfTopColorLayer = 4;
                        }
                    }
                 }
                    }
      
           else if(numberOfColorsInBottle == 3)
            {
                 if(bottleColors[2].Equals(bottleColors[1]))
                 {
                    numberOfTopColorLayer = 2;

                    if(bottleColors[1].Equals(bottleColors[0]))
                    {

                        numberOfTopColorLayer = 3;
     
                    }
                 }   
            }


           else if(numberOfColorsInBottle == 2)
            {
                 if(bottleColors[1].Equals(bottleColors[0]))
                 {
                    numberOfTopColorLayer = 2;
     
                 }   
            }

        rotationIndex = 3 - (numberOfColorsInBottle - numberOfTopColorLayer);
        }
  
    return numberOfTopColorLayer;
    }   

    public bool FillBottleCheck(Color colorToCheck)
    {
        if(numberOfColorsInBottle == 0)
        {
            return true;
        }
        else
        {
            if(numberOfColorsInBottle == 4)
            {
               return false;
            }
            else
            {
                if(topColor.Equals(colorToCheck))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }

    // Tính index góc quay dựa trên số ô trống của chai đích và số màu hiện tại
    private void CalculateRotationIndex(int numberOfEmptyspacesInSecondBottle)
    {
        rotationIndex = 3 - (numberOfColorsInBottle - Mathf.Min(numberOfEmptyspacesInSecondBottle,
                             numberOfTopColorLayer));
    }

    // Tăng mức chất lỏng trong chai (cập nhật shader)
    private void FillUp(float fillAmounToAdd)
    {
        bottleMaskSR.material.SetFloat("_FillAmount", bottleMaskSR.material.GetFloat("_FillAmount") + fillAmounToAdd - 0.001f);
    }

    // Xác định điểm xoay và chiều quay dựa vào vị trí tương đối giữa 2 chai
    private void chosenRotationPointAndDirection()
    {
        if(transform.position.x > bottleControllerRef.transform.position.x)
        {
            chosenRotationPoint = leftRotationPoint;
            directionMultiplaier = -1.0f;
        }
        else
        {
           chosenRotationPoint = rightRotationPoint;
            directionMultiplaier = 1.0f;
        }
    }

    IEnumerator LockBottle() // lock bottle when it is full  
    {
        yield return new WaitForEndOfFrame();
       
        if(bottleControllerRef.numberOfTopColorLayer == 4 
        && bottleControllerRef.numberOfColorsInBottle == 4)
        {
            bottleControllerRef.GetComponent<Collider2D>().enabled = false;
            bottleControllerRef.tag ="Locked Bottle";
        }
    }

    private void PlayBoilingSound() 
    {
        boilingSound.Play();
    }

    // Khóa toàn bộ chai - ngăn người chơi chạm vào chai khác khi đang đổ
    private void LockAll() // Cant move more than one bottle in the same time
    {
       levelbottles =  GameObject.FindGameObjectsWithTag("bottle");

         foreach (GameObject levelbottle in levelbottles) {
            levelbottle.GetComponent<Collider2D>().enabled = false;
        }
    }

    // Mở khóa toàn bộ chai - cho phép tương tác trở lại sau khi đổ xong
    private void UnlockAll()
    {
     levelbottles =  GameObject.FindGameObjectsWithTag("bottle");

         foreach (GameObject levelbottle in levelbottles) {
            levelbottle.GetComponent<Collider2D>().enabled = true;
        }

    }

    IEnumerator FixAmount() //sometimes during color transfer the transfered amount is not precise so this set it back to exact amount
    {
        yield return new WaitForEndOfFrame();

        if( bottleControllerRef.bottleMaskSR.material.GetFloat("_FillAmount") > 0.3f)
        {
            bottleControllerRef.bottleMaskSR.material.SetFloat("_FillAmount", 0.51f);
        }
               else if( bottleControllerRef.bottleMaskSR.material.GetFloat("_FillAmount") > -0.07f)
        {
            bottleControllerRef.bottleMaskSR.material.SetFloat("_FillAmount", 0.195f);
        }
               else  if( bottleControllerRef.bottleMaskSR.material.GetFloat("_FillAmount") > -0.385f)
        {
           bottleControllerRef.bottleMaskSR.material.SetFloat("_FillAmount", -0.12f);
        }
               else if( bottleControllerRef.bottleMaskSR.material.GetFloat("_FillAmount") > -0.70f)
        {
            bottleControllerRef.bottleMaskSR.material.SetFloat("_FillAmount", -0.435f);
        }


    }
}
