using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;


public class MaskCreator : EditorWindow {
    [MenuItem("Mask Creator/Mask Creator Tool")]
    public static void ShowWindow() {
         GetWindow<MaskCreator>("Mask Creator");
    }
    private ObjectField InputTextureField;
    private Vector2 TextureViewDimensions = new Vector2(340, 340);
    private Image OutputImage;
    private PopupField<string> InvertChannelField;
    private Toggle SquareToggle;
    
    public Texture2D InputTexture;
    public ComputeShader MainShader;
    public RenderTexture WorkingTexture;
    public RenderTexture TempWorkingTexture;
    public RenderTexture MaxCol;
    public RenderTexture MinCol;
    public RenderTexture BackgroundCol;
    private ComputeBuffer WorkQueA;
    private ComputeBuffer WorkQueB;
    private ComputeBuffer QueCount;
    private ComputeBuffer SeenBuffer;
    private int CurrentInputSelection = -1;
    private int InvertChannel = 3;
    private float Sharpness = 0;
    private bool Gradient = true;
    private bool Fill = false;
    private bool Invert = false;
    private bool Circle = false;
    private bool Square = false;
    private float ShapeSize = 1.0f;


    private int MaskInitializeKernel = 2;
    private int MaskChannelInvertKernel = 3;
    private int FillKernel = 4;
    private int FillTransferKernel = 5;
    private int OverlayKernel = 6;
    private int ClearKernel = 7;
    private int CleanKernel = 8;

    private VisualElement CreateVerticalBox(string Name) {
        VisualElement VertBox = new VisualElement();
        // VertBox.style.flexDirection = FlexDirection.Row;
        return VertBox;
    }

    private VisualElement CreateHorizontalBox(string Name) {
        VisualElement HorizBox = new VisualElement();
        HorizBox.style.flexDirection = FlexDirection.Row;
        return HorizBox;
    }

    private Rect CalcZoom(Vector2 InputUV, float ZoomAdjustment, Rect PrevUVs) {
        Rect CurrentUVs = PrevUVs;
        if(ZoomAdjustment > 0) ZoomAdjustment = 1.0f / 0.95f;
        else if(ZoomAdjustment < 0) ZoomAdjustment = 0.95f;
        InputUV.y = 1.0f - InputUV.y;
        CurrentUVs.position = PrevUVs.position - (ZoomAdjustment - 1.0f) * InputUV * PrevUVs.size;
        Vector2 Scale = new Vector2(PrevUVs.size.x, PrevUVs.size.y);
        Scale = Vector2.Min(Scale * ZoomAdjustment, Vector2.one);
        Vector2 OffsetCoords = CurrentUVs.position + Scale;
        Vector2 ChangeCoords = new Vector2(0,0);
        if(OffsetCoords.x > 1) ChangeCoords.x = 1 - OffsetCoords.x;
        if(OffsetCoords.y > 1) ChangeCoords.y = 1 - OffsetCoords.y;
        CurrentUVs.position = Vector2.Max(Vector2.Min(CurrentUVs.position + ChangeCoords, new Vector2(1,1)),new Vector2(0,0));
        CurrentUVs.size = Scale;
        return CurrentUVs;
    }

    private void CreateGUI() {
        if(MainShader == null) MainShader = Resources.Load<ComputeShader>("MaskCreatorShader");
        CreateRenderTexture(ref MaxCol, 40, 40);
        CreateRenderTexture(ref MinCol, 40, 40);
        CreateRenderTexture(ref BackgroundCol, 40, 40);
        VisualElement SettingsContainer = CreateHorizontalBox("Settings Container");
            VisualElement MaxColContainer = CreateVerticalBox("MaxCol Container");
                Label MaxColLabel = new Label("Max Color");
                Image MaxColImage = new Image() {image = MaxCol};
                    MaxColImage.style.maxWidth = 40;
                    MaxColImage.style.maxHeight = 40;     
                    MaxColImage.RegisterCallback<MouseDownEvent>(
                        e => {CurrentInputSelection = 0;}
                    );           
            MaxColContainer.Add(MaxColLabel);        
            MaxColContainer.Add(MaxColImage); 

            VisualElement MinColContainer = CreateVerticalBox("MinCol Container");
                Label MinColLabel = new Label("Min Color");
                Image MinColImage = new Image() {image = MinCol};
                    MinColImage.style.maxWidth = 40;
                    MinColImage.style.maxHeight = 40;                
                    MinColImage.RegisterCallback<MouseDownEvent>(
                        e => {CurrentInputSelection = 1;}
                    );           
            MinColContainer.Add(MinColLabel);        
            MinColContainer.Add(MinColImage); 

            VisualElement BackgroundColContainer = CreateVerticalBox("BackgroundCol Container");
                Label BackgroundColLabel = new Label("Background Color");
                VisualElement IntermediateContainer = CreateHorizontalBox("Intermediate Container");
                
                    Image BackgroundColImage = new Image() {image = BackgroundCol};
                        BackgroundColImage.style.maxWidth = 40;
                        BackgroundColImage.style.maxHeight = 40;                
                        BackgroundColImage.RegisterCallback<MouseDownEvent>(
                            e => {CurrentInputSelection = 2;}
                        );           
                        BackgroundColLabel.style.width = 150;
                    VisualElement IntermediateTextContainer = CreateVerticalBox("Intermediate Text Container");
                        Label IntermediateLabelA = new Label("<-Click Box");
                        Label IntermediateLabelB = new Label("Then");
                        Label IntermediateLabelC = new Label("Click Image Below");

                    IntermediateTextContainer.Add(IntermediateLabelA);
                    IntermediateTextContainer.Add(IntermediateLabelB);
                    IntermediateTextContainer.Add(IntermediateLabelC);
                IntermediateContainer.Add(BackgroundColImage);
                IntermediateContainer.Add(IntermediateTextContainer);
            BackgroundColContainer.Add(BackgroundColLabel);        
            BackgroundColContainer.Add(IntermediateContainer);        
            
            VisualElement ValueContainer = CreateVerticalBox("ValueContainer");
                VisualElement SharpnessContainer = CreateHorizontalBox("Sharpness Container");
                    Label SharpnessLabel = new Label("Sharpness: ");
                    Slider SharpnessSlider = new Slider() {value = Sharpness, highValue = 1.0f, lowValue = 0.0f};
                        SharpnessSlider.style.width = 100;
                        SharpnessSlider.RegisterValueChangedCallback(evt => {Sharpness = evt.newValue;});
                        SharpnessLabel.style.width = 75;
                SharpnessContainer.Add(SharpnessLabel);
                SharpnessContainer.Add(SharpnessSlider);
                
                VisualElement GradientContainer = CreateHorizontalBox("Gradient Container");
                    Label GradientLabel = new Label("Gradient: ");
                    Toggle GradientToggle = new Toggle() {value = Gradient};
                    GradientToggle.style.width = 100;
                    GradientToggle.style.height = 15;
                    GradientToggle.RegisterValueChangedCallback(evt => {Gradient = evt.newValue;});
                    GradientLabel.style.width = 75;
                GradientContainer.Add(GradientLabel);
                GradientContainer.Add(GradientToggle);


                VisualElement ButtonContainer = CreateHorizontalBox("Button Container");                    
                    Button InvertButton = new Button(() => {
                        MainShader.SetTexture(MaskChannelInvertKernel, "Result", WorkingTexture);
                        MainShader.SetInt("screen_width", InputTexture.width);
                        MainShader.SetInt("screen_height", InputTexture.height);
                        MainShader.SetInt("Channel", InvertChannel);
                        MainShader.Dispatch(MaskChannelInvertKernel, Mathf.CeilToInt((float)InputTexture.width / 32.0f), Mathf.CeilToInt((float)InputTexture.height / 32.0f), 1);
                        Graphics.Blit(WorkingTexture, TempWorkingTexture);
                        OutputImage.MarkDirtyRepaint();
                    }) {text = "Invert"};

                    Button CleanButton = new Button(() => {
                        Graphics.Blit(TempWorkingTexture, WorkingTexture);
                        MainShader.SetTexture(CleanKernel, "Result", WorkingTexture);
                        MainShader.SetTexture(CleanKernel, "Input", TempWorkingTexture);
                        MainShader.SetInt("screen_width", InputTexture.width);
                        MainShader.SetInt("screen_height", InputTexture.height);
                        MainShader.SetInt("Channel", InvertChannel);
                        MainShader.Dispatch(CleanKernel, Mathf.CeilToInt((float)InputTexture.width / 32.0f), Mathf.CeilToInt((float)InputTexture.height / 32.0f), 1);
                        Graphics.Blit(WorkingTexture, TempWorkingTexture);
                        OutputImage.MarkDirtyRepaint();
                    }) {text = "Clean"};

                    InvertChannelField = new PopupField<string>("Channel: ");
                    List<string> ChannelOptions = new List<string>();
                        ChannelOptions.Add("R");
                        ChannelOptions.Add("G");
                        ChannelOptions.Add("B");
                        ChannelOptions.Add("All");
                        InvertChannelField.choices = ChannelOptions;
                        InvertChannelField.index = InvertChannel;
                        InvertChannelField.RegisterValueChangedCallback(evt => {InvertChannel = InvertChannelField.index;});
                        InvertChannelField.ElementAt(0).style.minWidth = 45;
                        InvertChannelField.ElementAt(1).style.minWidth = 35;

                ButtonContainer.Add(InvertButton);
                ButtonContainer.Add(CleanButton);
                ButtonContainer.Add(InvertChannelField);

            ValueContainer.Add(SharpnessContainer);
            ValueContainer.Add(GradientContainer);
            ValueContainer.Add(ButtonContainer);

            VisualElement FillToggleContainer = CreateVerticalBox("Toggle Container");
                VisualElement FillContainer = CreateHorizontalBox("Fill Container");
                    Label FillLabel = new Label("Fill: ");                
                    Toggle FillToggle = new Toggle() {value = Fill};
                        FillToggle.style.width = 25;
                        FillToggle.style.height = 15;
                        FillToggle.RegisterValueChangedCallback(evt => {Fill = evt.newValue;});
                        FillLabel.style.width = 40;
                FillContainer.Add(FillLabel);
                FillContainer.Add(FillToggle);

                // VisualElement InvertContainer = CreateHorizontalBox("Invert Container");
                //     Label InvertLabel = new Label("Invert: ");                
                //     Toggle InvertToggle = new Toggle() {value = Invert};
                //         InvertToggle.style.width = 25;
                //         InvertToggle.style.height = 15;
                //         InvertToggle.RegisterValueChangedCallback(evt => {Invert = evt.newValue;});
                //         InvertLabel.style.width = 40;
                // InvertContainer.Add(InvertLabel);
                // InvertContainer.Add(InvertToggle);
            FillToggleContainer.Add(FillContainer);
            // FillToggleContainer.Add(InvertContainer);


            VisualElement ShapesToggleContainer = CreateVerticalBox("Shapes Container");
                VisualElement CircleContainer = CreateHorizontalBox("Circle Container");
                    Label CircleLabel = new Label("Circle: ");                
                    Toggle CircleToggle = new Toggle() {value = Circle};
                        CircleToggle.style.width = 25;
                        CircleToggle.style.height = 15;
                        CircleToggle.RegisterValueChangedCallback(evt => {Circle = evt.newValue; Square = !Circle; SquareToggle.value = !Circle;});
                        CircleLabel.style.width = 50;
                CircleContainer.Add(CircleLabel);
                CircleContainer.Add(CircleToggle);

                VisualElement SquareContainer = CreateHorizontalBox("Square Container");
                    Label SquareLabel = new Label("Square: ");                
                    SquareToggle = new Toggle() {value = Square};
                        SquareToggle.style.width = 25;
                        SquareToggle.style.height = 15;
                        SquareToggle.RegisterValueChangedCallback(evt => {Square = evt.newValue; Circle = !Square; CircleToggle.value = !Square;});
                        SquareLabel.style.width = 50;
                SquareContainer.Add(SquareLabel);
                SquareContainer.Add(SquareToggle);
            ShapesToggleContainer.Add(CircleContainer);
            ShapesToggleContainer.Add(SquareContainer);




        SettingsContainer.Add(MaxColContainer);
        SettingsContainer.Add(MinColContainer);
        SettingsContainer.Add(BackgroundColContainer);
        SettingsContainer.Add(ValueContainer);
        SettingsContainer.Add(FillToggleContainer);
        SettingsContainer.Add(ShapesToggleContainer);
        VisualElement TextureContainer = CreateHorizontalBox("Texture Container");
            #region InputTexture
            VisualElement InputContainer = CreateVerticalBox("Input Container"); 
                InputTextureField = new ObjectField() {value = InputTexture};
                Image InputImage = new Image() {image = InputTexture};
                    InputTextureField.objectType = typeof(Texture);
                    InputTextureField.label = "Input Image: ";
                    InputTextureField.RegisterValueChangedCallback(evt => {InputTexture = evt.newValue as Texture2D; InputImage.image = InputTexture;});
                    InputTextureField.style.width = 200;
                    InputTextureField.ElementAt(0).style.minWidth = 65;
                    InputTextureField.ElementAt(1).style.width = 45;

                    InputImage.style.maxWidth = TextureViewDimensions.x;
                    InputImage.style.maxHeight = TextureViewDimensions.y;
                    InputImage.style.minHeight = TextureViewDimensions.y;
                    InputImage.uv = new Rect(0,0,1, 1);
                    InputImage.RegisterCallback<WheelEvent>(
                        e => {
                            InputImage.uv = CalcZoom(
                                new Vector2(e.localMousePosition.x / TextureViewDimensions.x, e.localMousePosition.y / TextureViewDimensions.y),
                                e.mouseDelta.y,
                                InputImage.uv
                                );
                            InputImage.MarkDirtyRepaint();
                        }
                    );

                    InputImage.RegisterCallback<MouseDownEvent>(
                        e => {
                            if(CurrentInputSelection != -1) {
                                switch(CurrentInputSelection) {
                                    case 0: MainShader.SetTexture(1, "Result", MaxCol); break;
                                    case 1: MainShader.SetTexture(1, "Result", MinCol); break;
                                    case 2: MainShader.SetTexture(1, "Result", BackgroundCol); break;
                                }
                                MainShader.SetTexture(1, "Input", InputTexture);
                                MainShader.SetInt("screen_width", 40);
                                MainShader.SetInt("screen_height", 40);
                                MainShader.SetVector("SampledUV", Vector2.Scale(new Vector2(e.localMousePosition.x / TextureViewDimensions.x, 1.0f - (e.localMousePosition.y / TextureViewDimensions.y)), InputImage.uv.size) + InputImage.uv.position);
                                MainShader.Dispatch(1, Mathf.CeilToInt((float)40 / 32.0f), Mathf.CeilToInt((float)40 / 32.0f), 1);
                                CurrentInputSelection = -1;
                            } else {
                                CreateRenderTexture(ref WorkingTexture, InputTexture.width, InputTexture.height);
                                CreateRenderTexture(ref TempWorkingTexture, InputTexture.width, InputTexture.height);
                                MainShader.SetInt("screen_width", InputTexture.width);
                                MainShader.SetInt("screen_height", InputTexture.height);
                                MainShader.SetFloat("Sharpness", Sharpness);
                                MainShader.SetBool("Gradient", Gradient);
                                MainShader.SetTexture(MaskInitializeKernel, "Input", InputTexture);
                                MainShader.SetTexture(MaskInitializeKernel, "MaxCol", MaxCol);
                                MainShader.SetTexture(MaskInitializeKernel, "MinCol", MinCol);
                                MainShader.SetTexture(MaskInitializeKernel, "BackgroundCol", BackgroundCol);
                                MainShader.SetTexture(MaskInitializeKernel, "Result", WorkingTexture);
                                MainShader.Dispatch(MaskInitializeKernel, Mathf.CeilToInt((float)InputTexture.width / 32.0f), Mathf.CeilToInt((float)InputTexture.height / 32.0f), 1);
                                Graphics.Blit(WorkingTexture, TempWorkingTexture);
                                OutputImage.image = TempWorkingTexture;
                                OutputImage.MarkDirtyRepaint();
                            }
                        }
                    );
            InputContainer.Add(InputTextureField);
            InputContainer.Add(InputImage);
            #endregion
            #region OutputTexture
            VisualElement OutputContainer = CreateVerticalBox("Output Container"); 
                Button OutputButton = new Button(() => {
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = WorkingTexture;
                    Texture2D WorkingTexture2D = new Texture2D(WorkingTexture.width, WorkingTexture.height);
                    WorkingTexture2D.ReadPixels(new Rect(0, 0, WorkingTexture.width, WorkingTexture.height), 0, 0);
                    WorkingTexture2D.Apply();
                    RenderTexture.active = previous;
                    byte[] bytes = WorkingTexture2D.EncodeToPNG();
                    var dirPath = Application.dataPath + "/../Assets/";
                    if(!System.IO.Directory.Exists(dirPath)) {
                        Debug.Log("No Valid Folder");
                    } else {
                        System.IO.File.WriteAllBytes(dirPath + "Mask" + ".psd", bytes);
                        AssetDatabase.Refresh();
                    }
                }) {text = "Output Mask"};
                OutputButton.style.maxHeight = 20;
                OutputImage = new Image() {image = TempWorkingTexture};
                    OutputImage.style.maxWidth = TextureViewDimensions.x;
                    OutputImage.style.maxHeight = TextureViewDimensions.y;
                    OutputImage.style.minHeight = TextureViewDimensions.y;
                    OutputImage.uv = new Rect(0,0,1, 1);
                    OutputImage.RegisterCallback<WheelEvent>(
                        e => {
                            if(e.ctrlKey) {
                                ShapeSize -= e.mouseDelta.y;
                                if(!Fill && (Square || Circle)) {
                                    MainShader.SetInt("screen_width", WorkingTexture.width);
                                    MainShader.SetInt("screen_height", WorkingTexture.height);
                                    MainShader.SetFloat("ShapeSize", ShapeSize);
                                    MainShader.SetBool("Square", Square);
                                    MainShader.SetBool("Gradient", Gradient);
                                    MainShader.SetBool("Circle", Circle);
                                    MainShader.SetVector("SampledUV", Vector2.Scale(Vector2.Scale(new Vector2(e.localMousePosition.x / TextureViewDimensions.x, 1.0f - (e.localMousePosition.y / TextureViewDimensions.y)), OutputImage.uv.size) + OutputImage.uv.position, new Vector2(WorkingTexture.width, WorkingTexture.height)));
                                    MainShader.SetTexture(OverlayKernel, "Result", TempWorkingTexture);
                                    MainShader.SetTexture(OverlayKernel, "Input", WorkingTexture);
                                    MainShader.Dispatch(OverlayKernel, Mathf.CeilToInt((float)WorkingTexture.width / 32.0f), Mathf.CeilToInt((float)WorkingTexture.height / 32.0f), 1);
                                }
                            } else {
                                OutputImage.uv = CalcZoom(
                                    new Vector2(e.localMousePosition.x / TextureViewDimensions.x, e.localMousePosition.y / TextureViewDimensions.y),
                                    e.mouseDelta.y,
                                    OutputImage.uv
                                    );
                                OutputImage.MarkDirtyRepaint();
                            }
                        }
                    );

                    OutputImage.RegisterCallback<MouseDownEvent>(
                        e => {
                            if(e.pressedButtons == 1) {
                                if(Fill) {
                                    Vector2Int[] ActivePixels = new Vector2Int[WorkingTexture.width * WorkingTexture.height];
                                    CreateComputeBuffer(ref WorkQueB, ActivePixels);
                                    Vector2 SampledUV = Vector2.Scale(Vector2.Scale(new Vector2(e.localMousePosition.x / TextureViewDimensions.x, 1.0f - (e.localMousePosition.y / TextureViewDimensions.y)), OutputImage.uv.size) + OutputImage.uv.position, new Vector2(WorkingTexture.width, WorkingTexture.height));
                                    ActivePixels[0] = new Vector2Int((int)SampledUV.x, (int)SampledUV.y);
                                    CreateComputeBuffer(ref WorkQueA, ActivePixels);
                                    uint[] QueCountArray = new uint[3];
                                    QueCountArray[0] = 0;
                                    QueCountArray[1] = 1;
                                    QueCountArray[2] = 0;
                                    CreateComputeBuffer(ref QueCount, QueCountArray);
                                    int[] SeenArray = new int[WorkingTexture.width * WorkingTexture.height];
                                    CreateComputeBuffer(ref SeenBuffer, SeenArray);
                                    MainShader.SetInt("screen_width", WorkingTexture.width);
                                    MainShader.SetInt("screen_height", WorkingTexture.height);
                                    MainShader.SetTexture(FillKernel, "Result", WorkingTexture);
                                    MainShader.SetTexture(FillTransferKernel, "Result", WorkingTexture);
                                    MainShader.SetInt("Channel", InvertChannel);

                                    MainShader.SetBuffer(FillKernel, "SeenBuffer", SeenBuffer);
                                    MainShader.SetBuffer(FillKernel, "QueCountBuffer", QueCount);
                                    MainShader.SetBuffer(FillTransferKernel, "WorkQueA", WorkQueA);
                                    MainShader.SetBuffer(FillTransferKernel, "QueCountBuffer", QueCount);
                                        MainShader.SetBool("FirstPass", true);
                                        MainShader.SetBool("Iter", false);
                                        MainShader.Dispatch(FillTransferKernel, 1, 1, 1);
                                        MainShader.SetBool("FirstPass", false);
                                        int Iterations = Mathf.CeilToInt(Mathf.Sqrt(WorkingTexture.width * WorkingTexture.width + WorkingTexture.height * WorkingTexture.height));
                                    for(int i = 0; i < Iterations; i++) {
                                        MainShader.SetBool("Iter", (i % 2) == 1);
                                        MainShader.SetBuffer(FillKernel, "WorkQueA", (i % 2 == 0) ? WorkQueA : WorkQueB);
                                        MainShader.SetBuffer(FillKernel, "WorkQueB", (i % 2 == 1) ? WorkQueA : WorkQueB);
                                        MainShader.Dispatch(FillKernel, Mathf.CeilToInt((float)(WorkingTexture.width * WorkingTexture.height) / 1024.0f), 1, 1);
                                        MainShader.Dispatch(FillTransferKernel, 1, 1, 1);
                                    }
                                    Graphics.Blit(WorkingTexture, TempWorkingTexture);
                                    OutputImage.MarkDirtyRepaint();
                                } else {
                                    MainShader.SetInt("screen_width", WorkingTexture.width);
                                    MainShader.SetInt("screen_height", WorkingTexture.height);
                                    MainShader.SetFloat("ShapeSize", ShapeSize);
                                    MainShader.SetBool("Square", Square);
                                    MainShader.SetBool("Gradient", Gradient);
                                    MainShader.SetInt("Channel", InvertChannel);
                                    MainShader.SetBool("Circle", Circle);
                                    MainShader.SetVector("SampledUV", Vector2.Scale(Vector2.Scale(new Vector2(e.localMousePosition.x / TextureViewDimensions.x, 1.0f - (e.localMousePosition.y / TextureViewDimensions.y)), OutputImage.uv.size) + OutputImage.uv.position, new Vector2(WorkingTexture.width, WorkingTexture.height)));
                                    MainShader.SetTexture(ClearKernel, "Result", WorkingTexture);
                                    MainShader.Dispatch(ClearKernel, Mathf.CeilToInt((float)WorkingTexture.width / 32.0f), Mathf.CeilToInt((float)WorkingTexture.height / 32.0f), 1);
                                    Graphics.Blit(WorkingTexture, TempWorkingTexture);
                                    OutputImage.MarkDirtyRepaint();
                                }
                            }
                        }
                    );

                    OutputImage.RegisterCallback<MouseMoveEvent>(
                        e => {
                            if(!Fill) {
                                if(e.pressedButtons == 1) {
                                    MainShader.SetInt("screen_width", WorkingTexture.width);
                                    MainShader.SetInt("screen_height", WorkingTexture.height);
                                    MainShader.SetFloat("ShapeSize", ShapeSize);
                                    MainShader.SetBool("Square", Square);
                                    MainShader.SetBool("Gradient", Gradient);
                                    MainShader.SetInt("Channel", InvertChannel);
                                    MainShader.SetBool("Circle", Circle);
                                    MainShader.SetVector("SampledUV", Vector2.Scale(Vector2.Scale(new Vector2(e.localMousePosition.x / TextureViewDimensions.x, 1.0f - (e.localMousePosition.y / TextureViewDimensions.y)), OutputImage.uv.size) + OutputImage.uv.position, new Vector2(WorkingTexture.width, WorkingTexture.height)));
                                    MainShader.SetTexture(ClearKernel, "Result", WorkingTexture);
                                    MainShader.Dispatch(ClearKernel, Mathf.CeilToInt((float)WorkingTexture.width / 32.0f), Mathf.CeilToInt((float)WorkingTexture.height / 32.0f), 1);
                                    Graphics.Blit(WorkingTexture, TempWorkingTexture);
                                    OutputImage.MarkDirtyRepaint();
                                }
                                if((Square || Circle)) {
                                    MainShader.SetInt("screen_width", WorkingTexture.width);
                                    MainShader.SetInt("screen_height", WorkingTexture.height);
                                    MainShader.SetFloat("ShapeSize", ShapeSize);
                                    MainShader.SetBool("Square", Square);
                                    MainShader.SetBool("Gradient", Gradient);
                                    MainShader.SetBool("Circle", Circle);
                                    MainShader.SetVector("SampledUV", Vector2.Scale(Vector2.Scale(new Vector2(e.localMousePosition.x / TextureViewDimensions.x, 1.0f - (e.localMousePosition.y / TextureViewDimensions.y)), OutputImage.uv.size) + OutputImage.uv.position, new Vector2(WorkingTexture.width, WorkingTexture.height)));
                                    MainShader.SetTexture(OverlayKernel, "Result", TempWorkingTexture);
                                    MainShader.SetTexture(OverlayKernel, "Input", WorkingTexture);
                                    MainShader.Dispatch(OverlayKernel, Mathf.CeilToInt((float)WorkingTexture.width / 32.0f), Mathf.CeilToInt((float)WorkingTexture.height / 32.0f), 1);
                                }
                            }
                        }
                    );

                    OutputImage.RegisterCallback<MouseLeaveEvent>(
                        e => {
                            Graphics.Blit(WorkingTexture, TempWorkingTexture);
                            OutputImage.MarkDirtyRepaint();
                        }
                    );

            #endregion
            OutputContainer.Add(OutputButton);
            OutputContainer.Add(OutputImage);
        TextureContainer.Add(InputContainer);
        TextureContainer.Add(OutputContainer);
        
        VisualElement TopInfoContainer = CreateVerticalBox("Top Info Container");
            Label TextLabelA = new Label("Scroll wheel when hovering over the input texture or mask to zoom in/out");
            Label TextLabelB = new Label("Clicking on the input image texture will generate the mask");
            Label TextLabelC = new Label("Clicking on the max/min/background color boxes, then clicking on the input texture will act like a color eyedropper");
            Label TextLabelD = new Label("When fill is active, clicking on the mask will floodfill around the mouse");
            Label TextLabelE = new Label("When fill is NOT active, clicking on the mask will erase a section around the mouse(dictated by the Circle/Square toggle)");
            Label TextLabelF = new Label("When fill is NOT active, hovering over mask while holding ctrl and scrolling the mouse wheel, changes the size of the shape");
            Label TextLabelG = new Label("Sharpness changes how close to the Min/Max col a pixel can be to still be counted in the mask");
            Label TextLabelH = new Label("Channel changes which channel of the texture mask is being affected by whatever actions");
            Label TextLabelI = new Label("Clean will clean up tiny pixels(like 1 pixel junk) in the mask");
        TopInfoContainer.Add(TextLabelA);
        TopInfoContainer.Add(TextLabelB);
        TopInfoContainer.Add(TextLabelC);
        TopInfoContainer.Add(TextLabelD);
        TopInfoContainer.Add(TextLabelE);
        TopInfoContainer.Add(TextLabelF);
        TopInfoContainer.Add(TextLabelG);
        TopInfoContainer.Add(TextLabelH);
        TopInfoContainer.Add(TextLabelI);


        rootVisualElement.Add(SettingsContainer);
        rootVisualElement.Add(TextureContainer);
        rootVisualElement.Add(TopInfoContainer);
    }

    void OnDisable() {
        ReleaseSafe(ref WorkingTexture);
        ReleaseSafe(ref TempWorkingTexture);
        ReleaseSafe(ref MaxCol);
        ReleaseSafe(ref MinCol);
        ReleaseSafe(ref BackgroundCol);
        ReleaseSafe(ref WorkQueA);
        ReleaseSafe(ref WorkQueB);
        ReleaseSafe(ref SeenBuffer);
        ReleaseSafe(ref QueCount);
    }

    private void ReleaseSafe(ref RenderTexture Tex){if (Tex != null) Tex.Release();}
    private void ReleaseSafe(ref ComputeBuffer Buff) {if (Buff != null) {Buff.Release(); Buff = null;}}

    private void CreateRenderTexture(ref RenderTexture ThisTex, int Width, int Height) {
        if(ThisTex != null) ThisTex?.Release();
        ThisTex = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        ThisTex.enableRandomWrite = true;
        ThisTex.Create();
        MainShader.SetTexture(0, "Result", ThisTex);
        MainShader.SetInt("screen_width", Width);
        MainShader.SetInt("screen_height", Height);
        MainShader.Dispatch(0, Mathf.CeilToInt((float)Width / 32.0f), Mathf.CeilToInt((float)Height / 32.0f), 1);
    }

    private void CreateComputeBuffer<T>(ref ComputeBuffer buffer, T[] data)
    where T : struct
    {
        int stride = System.Runtime.InteropServices.Marshal.SizeOf<T>();
        if (buffer != null) {
            if (data == null || data.Length == 0 || !buffer.IsValid() || buffer.count != data.Length || buffer.stride != stride) {
                buffer.Release();
                buffer = null;
            }
        }
        if (data != null && data.Length != 0) {
            if (buffer == null) buffer = new ComputeBuffer(data.Length, stride);
            buffer.SetData(data);
        } else if (buffer == null) buffer = new ComputeBuffer(1, stride);
    }

    void Update() {
        if (EditorWindow.focusedWindow == this &&
            EditorWindow.mouseOverWindow == this) {
            this.Repaint();
        }
    }
}
