﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using CodeMonkey.Utils;

public class Graph_DynamicRESTFINAL : MonoBehaviour
{

    private static Graph_DynamicRESTFINAL instance;

    [SerializeField] private Sprite dotSprite;
    private RectTransform graphContainer;
    private RectTransform labelTemplateX;
    private RectTransform labelTemplateY;
    private RectTransform dashContainer;
    private RectTransform dashTemplateX;
    private RectTransform dashTemplateY;
    private List<GameObject> gameObjectList;
    private List<IGraphVisualObject> graphVisualObjectList;
    private GameObject tooltipGameObject;
    private List<RectTransform> yLabelList;
    private Dropdown dropdown;

    // Cached values
    private IGraphVisual graphVisual;
    private int maxVisibleValueAmount;
    private Func<int, string> getAxisLabelX;
    private List<String> labelsX;
    private Func<float, string> getAxisLabelY;
    private float xSize;
    private bool startYScaleAtZero;


    private bool useBarChart = true;

    [Header("Be sure that both scripts have the same attributes!")]
    [Header("Graph Data Detail")]

    [SerializeField, Tooltip("Phainomenon Variabe name: ")]
    private string variable = "Temperature";
    public String Variable_Name
    {
        get { return variable; }
        set { variable = value; }
    }

    [Serializable]
    public enum Unit
    {
        celcius, fahrenheit
    }

    [SerializeField, Tooltip("Unit of measurment: ")]
    private Unit unit = Unit.celcius;

    public Unit Set_Unit
    {
        get { return unit; }
        set { unit = value; }
    }

    [Header("Kafka REST Proxy Configuration")]
    [SerializeField, Tooltip("Set the Host URI: ")]
    private string HOST_WEB_URL = "http://eagle5.di.uoa.gr:8082/";
    public String Kafka_REST_Host_URI
    {
        get { return HOST_WEB_URL; }
        set { HOST_WEB_URL = value; }
    }

    //"consumers/drone_consumer/instances/drone_consumer_instance/records"
    [SerializeField, Tooltip("Set the Kafka REST Proxy cosumer name: ")]
    private string consumer = "consumer";
    public String Kafka_Consumer
    {
        get { return consumer; }
        set { consumer = value; }
    }
    [SerializeField, Tooltip("Set the Kafka REST Proxy cosumer instance name: ")]
    private string c_instance = "consumer_instance";
    public String Kafka_Consumer_Instance
    {
        get { return c_instance; }
        set { c_instance = value; }
    }

    IGraphVisual lineGraphVisual;
    IGraphVisual barChartVisual;
    string targetURI = "";
    List<int> valueList;
    List<string> keyList;
    bool celciousClicked = true;
    bool fahrenheitClicked = false;
    private GameObject status;
    private void Awake()
    {
        instance = this;
        // Grab base objects references
        graphContainer = transform.Find("graphContainer").GetComponent<RectTransform>();
        labelTemplateX = graphContainer.Find("labelTemplateX").GetComponent<RectTransform>();
        labelTemplateY = graphContainer.Find("labelTemplateY").GetComponent<RectTransform>();
        dashContainer = graphContainer.Find("dashContainer").GetComponent<RectTransform>();
        dashTemplateX = dashContainer.Find("dashTemplateX").GetComponent<RectTransform>();
        dashTemplateY = dashContainer.Find("dashTemplateY").GetComponent<RectTransform>();
        tooltipGameObject = graphContainer.Find("tooltip").gameObject;

        status = transform.Find("Status").gameObject;

        dropdown = transform.Find("Dropdown").GetComponent<Dropdown>();

        startYScaleAtZero = true;
        gameObjectList = new List<GameObject>();
        yLabelList = new List<RectTransform>();
        graphVisualObjectList = new List<IGraphVisualObject>();

        lineGraphVisual = new LineGraphVisual(graphContainer, dotSprite, Color.green, new Color(1, 1, 1, .5f));
        barChartVisual = new BarChartVisual(graphContainer, Color.green, .8f);

        transform.Find("barChartBtn").GetComponent<Button>().onClick.AddListener(delegate
        {
            SetGraphVisual(barChartVisual);
            useBarChart = true;
        });
        transform.Find("lineGraphBtn").GetComponent<Button>().onClick.AddListener(delegate
        {
            SetGraphVisual(lineGraphVisual);
            useBarChart = false;
        });
        transform.Find("Dropdown").GetComponent<Dropdown>().onValueChanged.AddListener(delegate
        {
            if(dropdown.value == 0){
                SetGetAxisLabelY((float _f) => "C" + Mathf.RoundToInt(_f)); 
            }else{
                SetGetAxisLabelY((float _f) => "F" + Mathf.RoundToInt(_f * 1.8f));
            }
        });
        /*
        transform.Find("decreaseVisibleAmountBtn").GetComponent<Button>().onClick.AddListener(delegate
        {
            DecreaseVisibleAmount();
        });
        transform.Find("increaseVisibleAmountBtn").GetComponent<Button>().onClick.AddListener(delegate
        {
            IncreaseVisibleAmount();
        });
        */
        /*
        transform.Find("celciusBtn").GetComponent<Button>().onClick.AddListener(delegate
        {
            celciousClicked = true;
            fahrenheitClicked = false;
            SetGetAxisLabelY((float _f) => "C" + Mathf.RoundToInt(_f));
        });

        transform.Find("fahrenheitBtn").GetComponent<Button>().onClick.AddListener(delegate
        {
            fahrenheitClicked = true;
            celciousClicked = false;
            SetGetAxisLabelY((float _f) => "F" + Mathf.RoundToInt(_f * 1.8f));
        });
        */

                //"consumers/drone_consumer/instances/drone_consumer_instance/records"
        targetURI = HOST_WEB_URL + "consumers/" + consumer
            + "/instances/" + c_instance + "/records/";

        switch (unit)
        {
            case Unit.celcius: dropdown.value = 0; break;
            case Unit.fahrenheit: dropdown.value = 1; break;
        }

        HideTooltip();

        //HERE WE HAVE TO ADD THE FUNCTIONALITY THAT UPDATES THE VALUES
        // Set up base values
        //the two lists that folow have to have the same length
        StartCoroutine(GetKafkaMessages());

        keyList = new List<String>() { "ROAD", "AIR", "WATER", "CLOUDS" };

        if(valueList == null){
            valueList = new List<int>() { 5, 98, 56, 32 };
        }
        
        //RAWFIE
        //labelsX = new List<String>() { "ROAD", "AIR", "WATER", "CLOUDS" };

        


    }

    private IEnumerator GetKafkaMessages()
    {
        string targetURI = HOST_WEB_URL + "consumers/"+consumer+"/instances/"+c_instance+"/records";

        while (true)
        {
            var request = new UnityWebRequest(targetURI, "GET");
            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/vnd.kafka.json.v2+json");
            request.SendWebRequest();

            yield return new WaitUntil(() => request.isDone);

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.LogError(request.error);
                System.Diagnostics.Debug.Write(request.error);
            }
            else
            {
                //Debug.Log("GET complete!");
                //Debug.Log(request.responseCode);
                //Debug.Log(request.downloadHandler.data.ToString());
                string response = System.Text.Encoding.UTF8.GetString(request.downloadHandler.data);
                var data = request.downloadHandler.text;
                //String response = request.downloadHandler.text;
                //Debug.LogError(data);
                //response.Remove(0);
                //response.Remove(response.Length-2);
                
                //Debug.Log(response.Length);
                if(response.Length > 500)
                {
                    continue;
                }

                if(response.Length == 0)
                {
                    continue;
                }


                //Debug.Log(response);
                //Response responseData = JsonUtility.FromJsonOverwrite(response);
                
                //this is automatically set to an array
                JSONObject responseJson = new JSONObject(response);
                //accessData(responseJson);
                if (responseJson == null)
                {
                    continue;
                }
                //accessData(responseJson);

                JSONObject vectorMsg = null;
                foreach(JSONObject obj in responseJson.list){
                    string topic = obj.GetField("topic").ToString();
                    topic.Remove(0);
                    topic.Remove(topic.Length-1);
                    Debug.Log(topic);
                    //VectorTopic
                    if(topic.Equals("\"VectorTopic\"")){
                        Debug.Log(obj.GetField("topic"));
                        JSONObject temp = obj;
                        vectorMsg = temp.GetField("value");
                    }
			    }

                valueList = new List<int>();
                if(vectorMsg != null){
                    //we have data
                    status.SetActive(false);
                    Debug.Log(vectorMsg);
                    this.keyList = vectorMsg.keys;
                    for(int i=0; i<keyList.Count; i++){
                        int val = 0;
                        Debug.Log(keyList[i]);
                        string valstr = vectorMsg.GetField(keyList[i]).ToString();
                        val = int.Parse(valstr);
                        Debug.Log(val);
                        valueList.Add(val);
                    }

                    if (useBarChart)
                    {

                        if (dropdown.value == 0)
                        {
                            ShowGraph(valueList, barChartVisual, -1, (int _i) => keyList[_i], (float _f) => "C" + Mathf.RoundToInt(_f));
                        }
                        else
                        {
                            ShowGraph(valueList, barChartVisual, -1, (int _i) => keyList[_i], (float _f) => "F" + Mathf.RoundToInt(_f * 1.8f));
                        }
                    }
                    else
                    {
                        if (dropdown.value == 0)
                        {
                            ShowGraph(valueList, lineGraphVisual, -1, (int _i) => keyList[_i], (float _f) => "C" + Mathf.RoundToInt(_f));
                        }
                        else
                        {
                            ShowGraph(valueList, lineGraphVisual, -1, (int _i) => keyList[_i], (float _f) => "F" + Mathf.RoundToInt(_f * 1.8f));
                        }
                    }
                }
            }
        }
    }

    public static void ShowTooltip_Static(string tooltipText, Vector2 anchoredPosition)
    {
        instance.ShowTooltip(tooltipText, anchoredPosition);
    }

    private void ShowTooltip(string tooltipText, Vector2 anchoredPosition)
    {
        // Show Tooltip GameObject
        tooltipGameObject.SetActive(true);

        tooltipGameObject.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;

        Text tooltipUIText = tooltipGameObject.transform.Find("text").GetComponent<Text>();
        tooltipUIText.text = tooltipText;

        float textPaddingSize = 4f;
        Vector2 backgroundSize = new Vector2(
            tooltipUIText.preferredWidth + textPaddingSize * 2f,
            tooltipUIText.preferredHeight + textPaddingSize * 2f
        );

        tooltipGameObject.transform.Find("background").GetComponent<RectTransform>().sizeDelta = backgroundSize;

        // UI Visibility Sorting based on Hierarchy, SetAsLastSibling in order to show up on top
        tooltipGameObject.transform.SetAsLastSibling();
    }

    public static void HideTooltip_Static()
    {
        instance.HideTooltip();
    }

    private void HideTooltip()
    {
        tooltipGameObject.SetActive(false);
    }

    private void SetGetAxisLabelX(Func<int, string> getAxisLabelX)
    {
        ShowGraph(this.valueList, this.graphVisual, this.maxVisibleValueAmount, getAxisLabelX, this.getAxisLabelY);
    }

    private void SetGetAxisLabelY(Func<float, string> getAxisLabelY)
    {
        ShowGraph(this.valueList, this.graphVisual, this.maxVisibleValueAmount, this.getAxisLabelX, getAxisLabelY);
    }

    private void IncreaseVisibleAmount()
    {
        ShowGraph(this.valueList, this.graphVisual, this.maxVisibleValueAmount + 1, this.getAxisLabelX, this.getAxisLabelY);
    }

    private void DecreaseVisibleAmount()
    {
        ShowGraph(this.valueList, this.graphVisual, this.maxVisibleValueAmount - 1, this.getAxisLabelX, this.getAxisLabelY);
    }

    private void SetGraphVisual(IGraphVisual graphVisual)
    {
        ShowGraph(this.valueList, graphVisual, this.maxVisibleValueAmount, this.getAxisLabelX, this.getAxisLabelY);
    }

    private void ShowGraph(List<int> valueList, IGraphVisual graphVisual, int maxVisibleValueAmount = -1, Func<int, string> getAxisLabelX = null, Func<float, string> getAxisLabelY = null)
    {
        this.valueList = valueList;
        this.graphVisual = graphVisual;
        this.getAxisLabelX = getAxisLabelX;
        this.getAxisLabelY = getAxisLabelY;

        if (maxVisibleValueAmount <= 0)
        {
            // Show all if no amount specified
            maxVisibleValueAmount = valueList.Count;
        }
        if (maxVisibleValueAmount > valueList.Count)
        {
            // Validate the amount to show the maximum
            maxVisibleValueAmount = valueList.Count;
        }

        this.maxVisibleValueAmount = maxVisibleValueAmount;

        // Test for label defaults
        if (getAxisLabelX == null)
        {
            getAxisLabelX = delegate (int _dataset) {
                if(_dataset == 0 || _dataset > labelsX.Count - 1)
                {
                    return "Unknown";
                }
                return labelsX[_dataset];
            };
        }
        if (getAxisLabelY == null)
        {
            getAxisLabelY = delegate (float _f) { return Mathf.RoundToInt(_f).ToString(); };
        }

        // Clean up previous graph
        foreach (GameObject gameObject in gameObjectList)
        {
            Destroy(gameObject);
        }
        gameObjectList.Clear();
        yLabelList.Clear();

        foreach (IGraphVisualObject graphVisualObject in graphVisualObjectList)
        {
            graphVisualObject.CleanUp();
        }
        graphVisualObjectList.Clear();

        //in this case the function below cuases an exception as null referenced
        //graphVisual.CleanUp();

        // Grab the width and height from the container
        float graphWidth = graphContainer.sizeDelta.x;
        float graphHeight = graphContainer.sizeDelta.y;

        float yMinimum, yMaximum;
        CalculateYScale(out yMinimum, out yMaximum);

        // Set the distance between each point on the graph 
        xSize = graphWidth / (maxVisibleValueAmount + 1);

        // Cycle through all visible data points
        int xIndex = 0;
        for (int i = Mathf.Max(valueList.Count - maxVisibleValueAmount, 0); i < valueList.Count; i++)
        {
            float xPosition = xSize + xIndex * xSize;
            float yPosition = ((valueList[i] - yMinimum) / (yMaximum - yMinimum)) * graphHeight;

            // Add data point visual
            string tooltipText = getAxisLabelY(valueList[i]);
            IGraphVisualObject graphVisualObject = graphVisual.CreateGraphVisualObject(new Vector2(xPosition, yPosition), xSize, tooltipText);
            graphVisualObjectList.Add(graphVisualObject);

            // Duplicate the x label template
            RectTransform labelX = Instantiate(labelTemplateX);
            labelX.SetParent(graphContainer, false);
            labelX.gameObject.SetActive(true);
            labelX.anchoredPosition = new Vector2(xPosition, -20f);
            labelX.GetComponent<Text>().text = getAxisLabelX(i);
            gameObjectList.Add(labelX.gameObject);

            // Duplicate the x dash template
            RectTransform dashX = Instantiate(dashTemplateX);
            dashX.SetParent(dashContainer, false);
            dashX.gameObject.SetActive(true);
            dashX.anchoredPosition = new Vector2(xPosition, -5f);
            gameObjectList.Add(dashX.gameObject);

            xIndex++;
        }

        // Set up separators on the y axis
        int separatorCount = 10;
        for (int i = 0; i <= separatorCount; i++)
        {
            // Duplicate the label template
            RectTransform labelY = Instantiate(labelTemplateY);
            labelY.SetParent(graphContainer, false);
            labelY.gameObject.SetActive(true);
            float normalizedValue = i * 1f / separatorCount;
            labelY.anchoredPosition = new Vector2(-10f, normalizedValue * graphHeight);
            labelY.GetComponent<Text>().text = getAxisLabelY(yMinimum + (normalizedValue * (yMaximum - yMinimum)));
            yLabelList.Add(labelY);
            gameObjectList.Add(labelY.gameObject);

            // Duplicate the dash template
            RectTransform dashY = Instantiate(dashTemplateY);
            dashY.SetParent(dashContainer, false);
            dashY.gameObject.SetActive(true);
            dashY.anchoredPosition = new Vector2(-10f, normalizedValue * graphHeight);
            gameObjectList.Add(dashY.gameObject);
        }
    }

    private void UpdateValues(int index, int dang_value)
    {
        float yMinimumBefore, yMaximumBefore;
        CalculateYScale(out yMinimumBefore, out yMaximumBefore);

        valueList[index] = dang_value;

        float graphWidth = graphContainer.sizeDelta.x;
        float graphHeight = graphContainer.sizeDelta.y;

        float yMinimum, yMaximum;
        CalculateYScale(out yMinimum, out yMaximum);

        bool yScaleChanged = yMinimumBefore != yMinimum || yMaximumBefore != yMaximum;

        if (!yScaleChanged && index >= 0)
        {
            // Y Scale did not change, update only this value
            float xPosition = xSize + index * xSize;
            float yPosition = ((dang_value - yMinimum) / (yMaximum - yMinimum)) * graphHeight;

            // Add data point visual
            string tooltipText = getAxisLabelY(dang_value);
            graphVisualObjectList[index].SetGraphVisualObjectInfo(new Vector2(xPosition, yPosition), xSize, tooltipText);
            // Debug.LogError(index);
        }
        else
        {
            // Y scale changed, update whole graph and y axis labels
            // Cycle through all visible data points
            int xIndex = 0;
            for (int i = Mathf.Max(valueList.Count - maxVisibleValueAmount, 0); i < valueList.Count; i++)
            {
                float xPosition = xSize + xIndex * xSize;
                float yPosition = ((valueList[i] - yMinimum) / (yMaximum - yMinimum)) * graphHeight;

                // Add data point visual
                string tooltipText = getAxisLabelY(valueList[i]);
                graphVisualObjectList[xIndex].SetGraphVisualObjectInfo(new Vector2(xPosition, yPosition), xSize, tooltipText);

                xIndex++;
            }

            for (int i = 0; i < yLabelList.Count; i++)
            {
                float normalizedValue = i * 1f / yLabelList.Count;
                yLabelList[i].GetComponent<Text>().text = getAxisLabelY(yMinimum + (normalizedValue * (yMaximum - yMinimum)));
            }
        }
    }

    private void CalculateYScale(out float yMinimum, out float yMaximum)
    {

        if (valueList.Count == 0)
        {
            yMaximum = 0;
            yMinimum = 0;
            return;
        }
        // Identify y Min and Max values
        yMaximum = valueList[0];
        yMinimum = valueList[0];

        for (int i = Mathf.Max(valueList.Count - maxVisibleValueAmount, 0); i < valueList.Count; i++)
        {
            int value = valueList[i];
            if (value > yMaximum)
            {
                yMaximum = value;
            }
            if (value < yMinimum)
            {
                yMinimum = value;
            }
        }

        float yDifference = yMaximum - yMinimum;
        if (yDifference <= 0)
        {
            yDifference = 5f;
        }
        yMaximum = yMaximum + (yDifference * 0.2f);
        yMinimum = yMinimum - (yDifference * 0.2f);

        if (startYScaleAtZero)
        {
            yMinimum = 0f; // Start the graph at zero
        }
    }



    /*
     * Interface definition for showing visual for a data point
     * */
    private interface IGraphVisual
    {

        IGraphVisualObject CreateGraphVisualObject(Vector2 graphPosition, float graphPositionWidth, string tooltipText);
        void CleanUp();

    }

    /*
     * Represents a single Visual Object in the graph
     * */
    private interface IGraphVisualObject
    {

        void SetGraphVisualObjectInfo(Vector2 graphPosition, float graphPositionWidth, string tooltipText);
        void CleanUp();

    }


    /*
     * Displays data points as a Bar Chart
     * */
    private class BarChartVisual : IGraphVisual
    {

        private RectTransform graphContainer;
        private Color barColor;
        private float barWidthMultiplier;

        public BarChartVisual(RectTransform graphContainer, Color barColor, float barWidthMultiplier)
        {
            this.graphContainer = graphContainer;
            this.barColor = barColor;
            this.barWidthMultiplier = barWidthMultiplier;
        }

        public void CleanUp()
        {
        }

        public IGraphVisualObject CreateGraphVisualObject(Vector2 graphPosition, float graphPositionWidth, string tooltipText)
        {
            GameObject barGameObject = CreateBar(graphPosition, graphPositionWidth);

            BarChartVisualObject barChartVisualObject = new BarChartVisualObject(barGameObject, barWidthMultiplier);
            barChartVisualObject.SetGraphVisualObjectInfo(graphPosition, graphPositionWidth, tooltipText);

            return barChartVisualObject;
        }

        private GameObject CreateBar(Vector2 graphPosition, float barWidth)
        {
            GameObject gameObject = new GameObject("bar", typeof(Image));
            gameObject.transform.SetParent(graphContainer, false);
            gameObject.GetComponent<Image>().color = barColor;
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(graphPosition.x, 0f);
            rectTransform.sizeDelta = new Vector2(barWidth * barWidthMultiplier, graphPosition.y);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);
            rectTransform.pivot = new Vector2(.5f, 0f);

            // Add Button_UI Component which captures UI Mouse Events
            Utilities.Button_UI barButtonUI = gameObject.AddComponent<Utilities.Button_UI>();

            return gameObject;
        }


        public class BarChartVisualObject : IGraphVisualObject
        {

            private GameObject barGameObject;
            private float barWidthMultiplier;

            public BarChartVisualObject(GameObject barGameObject, float barWidthMultiplier)
            {
                this.barGameObject = barGameObject;
                this.barWidthMultiplier = barWidthMultiplier;
            }

            public void SetGraphVisualObjectInfo(Vector2 graphPosition, float graphPositionWidth, string tooltipText)
            {
                RectTransform rectTransform = barGameObject.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = new Vector2(graphPosition.x, 0f);
                rectTransform.sizeDelta = new Vector2(graphPositionWidth * barWidthMultiplier, graphPosition.y);


                //Button_UI is used HERE!!!!!!!!
                //DO NOT DELETE IT
                Utilities.Button_UI barButtonUI = barGameObject.GetComponent<Utilities.Button_UI>();

                // Show Tooltip on Mouse Over
                barButtonUI.MouseOverOnceFunc = () => {
                    ShowTooltip_Static(tooltipText, graphPosition);
                };

                // Hide Tooltip on Mouse Out
                barButtonUI.MouseOutOnceFunc = () => {
                    HideTooltip_Static();
                };
            }

            public void CleanUp()
            {
                Destroy(barGameObject);
            }


        }

    }


    /*
     * Displays data points as a Line Graph
     * */
    private class LineGraphVisual : IGraphVisual
    {

        private RectTransform graphContainer;
        private Sprite dotSprite;
        private LineGraphVisualObject lastLineGraphVisualObject;
        private Color dotColor;
        private Color dotConnectionColor;

        public LineGraphVisual(RectTransform graphContainer, Sprite dotSprite, Color dotColor, Color dotConnectionColor)
        {
            this.graphContainer = graphContainer;
            this.dotSprite = dotSprite;
            this.dotColor = dotColor;
            this.dotConnectionColor = dotConnectionColor;
            lastLineGraphVisualObject = null;
        }

        public void CleanUp()
        {
            lastLineGraphVisualObject = null;
        }


        public IGraphVisualObject CreateGraphVisualObject(Vector2 graphPosition, float graphPositionWidth, string tooltipText)
        {
            GameObject dotGameObject = CreateDot(graphPosition);


            GameObject dotConnectionGameObject = null;
            if (lastLineGraphVisualObject != null)
            {
                dotConnectionGameObject = CreateDotConnection(lastLineGraphVisualObject.GetGraphPosition(), dotGameObject.GetComponent<RectTransform>().anchoredPosition);
            }

            LineGraphVisualObject lineGraphVisualObject = new LineGraphVisualObject(dotGameObject, dotConnectionGameObject, lastLineGraphVisualObject);
            lineGraphVisualObject.SetGraphVisualObjectInfo(graphPosition, graphPositionWidth, tooltipText);

            //A TRICK BECAUSE SOMETHING DOES NOT WORK AS IT SHOULD
            //taking the last dot position each time as a reference for the tooltip position
            //lineGraphVisualObject.SetGraphVisualObjectInfo(lastLineGraphVisualObject.GetGraphPosition(), graphPositionWidth, tooltipText);

            lastLineGraphVisualObject = lineGraphVisualObject;

            return lineGraphVisualObject;
        }

        private GameObject CreateDot(Vector2 anchoredPosition)
        {
            GameObject gameObject = new GameObject("dot", typeof(Image));
            gameObject.transform.SetParent(graphContainer, false);
            gameObject.GetComponent<Image>().sprite = dotSprite;
            gameObject.GetComponent<Image>().color = dotColor;
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(11, 11);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);

            // Add Button_UI Component which captures UI Mouse Events
            Utilities.Button_UI dotButtonUI = gameObject.AddComponent<Utilities.Button_UI>();

            return gameObject;
        }

        private GameObject CreateDotConnection(Vector2 dotPositionA, Vector2 dotPositionB)
        {
            GameObject gameObject = new GameObject("dotConnection", typeof(Image));
            gameObject.transform.SetParent(graphContainer, false);
            gameObject.GetComponent<Image>().color = dotConnectionColor;
            gameObject.GetComponent<Image>().raycastTarget = false;
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            Vector2 dir = (dotPositionB - dotPositionA).normalized;
            float distance = Vector2.Distance(dotPositionA, dotPositionB);
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(0, 0);
            rectTransform.sizeDelta = new Vector2(distance, 3f);
            rectTransform.anchoredPosition = dotPositionA + dir * distance * .5f;
            rectTransform.localEulerAngles = new Vector3(0, 0, UtilsClass.GetAngleFromVectorFloat(dir));
            return gameObject;
        }


        public class LineGraphVisualObject : IGraphVisualObject
        {

            public event EventHandler OnChangedGraphVisualObjectInfo;

            private GameObject dotGameObject;
            private GameObject dotConnectionGameObject;
            private LineGraphVisualObject lastVisualObject;

            public LineGraphVisualObject(GameObject dotGameObject, GameObject dotConnectionGameObject, LineGraphVisualObject lastVisualObject)
            {
                this.dotGameObject = dotGameObject;
                this.dotConnectionGameObject = dotConnectionGameObject;
                this.lastVisualObject = lastVisualObject;

                if (lastVisualObject != null)
                {
                    lastVisualObject.OnChangedGraphVisualObjectInfo += LastVisualObject_OnChangedGraphVisualObjectInfo;
                }
            }

            private void LastVisualObject_OnChangedGraphVisualObjectInfo(object sender, EventArgs e)
            {
                UpdateDotConnection();
            }

            public void SetGraphVisualObjectInfo(Vector2 graphPosition, float graphPositionWidth, string tooltipText)
            {
                RectTransform rectTransform = dotGameObject.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = graphPosition;

                UpdateDotConnection();

                Utilities.Button_UI dotButtonUI = dotGameObject.GetComponent<Utilities.Button_UI>();

                // Show Tooltip on Mouse Over
                dotButtonUI.MouseOverOnceFunc = () => {
                    ShowTooltip_Static(tooltipText, graphPosition);
                };

                // Hide Tooltip on Mouse Out
                dotButtonUI.MouseOutOnceFunc = () => {
                    HideTooltip_Static();
                };

                if (OnChangedGraphVisualObjectInfo != null) OnChangedGraphVisualObjectInfo(this, EventArgs.Empty);
            }

            public void CleanUp()
            {
                Destroy(dotGameObject);
                Destroy(dotConnectionGameObject);
            }

            public Vector2 GetGraphPosition()
            {
                RectTransform rectTransform = dotGameObject.GetComponent<RectTransform>();
                return rectTransform.anchoredPosition;
            }

            private void UpdateDotConnection()
            {
                if (dotConnectionGameObject != null)
                {
                    RectTransform dotConnectionRectTransform = dotConnectionGameObject.GetComponent<RectTransform>();
                    Vector2 dir = (lastVisualObject.GetGraphPosition() - GetGraphPosition()).normalized;
                    float distance = Vector2.Distance(GetGraphPosition(), lastVisualObject.GetGraphPosition());
                    dotConnectionRectTransform.sizeDelta = new Vector2(distance, 3f);
                    dotConnectionRectTransform.anchoredPosition = GetGraphPosition() + dir * distance * .5f;
                    dotConnectionRectTransform.localEulerAngles = new Vector3(0, 0, UtilsClass.GetAngleFromVectorFloat(dir));
                }
            }

        }

    }

}