﻿using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnboundLib.Utils.UI
{

    public static class MainMenuLinks
    {

        private static GameObject _Links = null;

        public static GameObject Links
        {
            get
            {
                if (_Links != null) { return _Links; }
                
                _Links = UnityEngine.GameObject.Instantiate(Unbound.linkAssets.LoadAsset<GameObject>("Links"), MainMenuHandler.instance.transform.Find("Canvas/"));
                UnityEngine.GameObject.DontDestroyOnLoad(_Links);
                // do setup like placement and adding components
                _Links.transform.position = MainCam.instance.transform.GetComponent<Camera>().ScreenToWorldPoint(new Vector3(Screen.height*16f/9f, 0, 0f));
                _Links.transform.position += new Vector3(0f, 0f, 100f);

                Link discordLink = _Links.transform.GetChild(0).gameObject.AddComponent<Link>();
                discordLink.link = "https://discord.gg/zUtsjXWeWk";
                Link thunderstoreLink = _Links.transform.GetChild(1).gameObject.AddComponent<Link>();
                thunderstoreLink.link = "https://rounds.thunderstore.io/?ordering=most-downloaded";
                return _Links;
            }
        }
        public static GameObject ROUNDSModding => Links.transform.GetChild(0).gameObject;
        public static GameObject ROUNDSThunderstore => Links.transform.GetChild(1).gameObject;

        public static void AddLinks(bool firstTime)
        {
            Unbound.Instance.ExecuteAfterSeconds(firstTime ? 0.2f : 0f, () =>
            {
                Links.SetActive(true);
            });
        }
        public static void HideLinks()
        {
            Links.SetActive(false);
        }
    }

    public class Link : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public string link = "";
        private const float hoverScale = 1.05f;
        private const float clickScale = 0.95f;
        private Vector3 defaultScale;
        private bool inBounds = false;
        private bool pressed = false;
        void Start()
        {
            defaultScale = this.gameObject.transform.localScale;
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (inBounds)
            {
                pressed = true;
                this.gameObject.transform.localScale = defaultScale * clickScale;
            }
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (inBounds && pressed)
            {
                Application.OpenURL(link);
            }
            pressed = false;
            if (!inBounds)
            {
                this.gameObject.transform.localScale = defaultScale;
            }
            else
            {
                this.gameObject.transform.localScale = defaultScale * hoverScale;
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            inBounds = true;
            this.gameObject.transform.localScale = defaultScale * hoverScale;
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            inBounds = false;
            if (!pressed)
            {
                this.gameObject.transform.localScale = defaultScale;
            }
        }
    }

    [RequireComponent(typeof(TextMeshProUGUI))]
    public class OpenHyperlinks : MonoBehaviour, IPointerClickHandler
    {
        public bool doesColorChangeOnHover = true;
        public Color hoverColor = new Color(60f / 255f, 120f / 255f, 1f);

        private TextMeshProUGUI pTextMeshPro;
        private Canvas pCanvas;
        private Camera pCamera;

        public bool isLinkHighlighted { get { return pCurrentLink != -1; } }

        private int pCurrentLink = -1;
        private List<Color32[]> pOriginalVertexColors = new List<Color32[]>();

        protected virtual void Awake()
        {
            pTextMeshPro = GetComponent<TextMeshProUGUI>();
            pCanvas = GetComponentInParent<Canvas>();

            // Get a reference to the camera if Canvas Render Mode is not ScreenSpace Overlay.
            if (pCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                pCamera = null;
            else
                pCamera = pCanvas.worldCamera;
        }

        void LateUpdate()
        {
            // is the cursor in the correct region (above the text area) and furthermore, in the link region?
            var isHoveringOver = TMP_TextUtilities.IsIntersectingRectTransform(pTextMeshPro.rectTransform, Input.mousePosition, pCamera);
            int linkIndex = isHoveringOver ? TMP_TextUtilities.FindIntersectingLink(pTextMeshPro, Input.mousePosition, pCamera)
                : -1;

            // Clear previous link selection if one existed.
            if (pCurrentLink != -1 && linkIndex != pCurrentLink)
            {
                // Debug.Log("Clear old selection");
                SetLinkToColor(pCurrentLink, (linkIdx, vertIdx) => pOriginalVertexColors[linkIdx][vertIdx]);
                pOriginalVertexColors.Clear();
                pCurrentLink = -1;
            }

            // Handle new link selection.
            if (linkIndex != -1 && linkIndex != pCurrentLink)
            {
                // Debug.Log("New selection");
                pCurrentLink = linkIndex;
                if (doesColorChangeOnHover)
                    pOriginalVertexColors = SetLinkToColor(linkIndex, (_linkIdx, _vertIdx) => hoverColor);
            }

            // Debug.Log(string.Format("isHovering: {0}, link: {1}", isHoveringOver, linkIndex));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Debug.Log("Click at POS: " + eventData.position + "  World POS: " + eventData.worldPosition);

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(pTextMeshPro, Input.mousePosition, pCamera);
            if (linkIndex != -1)
            { // was a link clicked?
                TMP_LinkInfo linkInfo = pTextMeshPro.textInfo.linkInfo[linkIndex];

                // Debug.Log(string.Format("id: {0}, text: {1}", linkInfo.GetLinkID(), linkInfo.GetLinkText()));
                // open the link id as a url, which is the metadata we added in the text field
                Application.OpenURL(linkInfo.GetLinkID());
            }
        }

        List<Color32[]> SetLinkToColor(int linkIndex, Func<int, int, Color32> colorForLinkAndVert)
        {
            TMP_LinkInfo linkInfo = pTextMeshPro.textInfo.linkInfo[linkIndex];

            var oldVertColors = new List<Color32[]>(); // store the old character colors

            for (int i = 0; i < linkInfo.linkTextLength; i++)
            { // for each character in the link string
                int characterIndex = linkInfo.linkTextfirstCharacterIndex + i; // the character index into the entire text
                var charInfo = pTextMeshPro.textInfo.characterInfo[characterIndex];
                int meshIndex = charInfo.materialReferenceIndex; // Get the index of the material / sub text object used by this character.
                int vertexIndex = charInfo.vertexIndex; // Get the index of the first vertex of this character.

                Color32[] vertexColors = pTextMeshPro.textInfo.meshInfo[meshIndex].colors32; // the colors for this character
                oldVertColors.Add(vertexColors.ToArray());

                if (charInfo.isVisible)
                {
                    vertexColors[vertexIndex + 0] = colorForLinkAndVert(i, vertexIndex + 0);
                    vertexColors[vertexIndex + 1] = colorForLinkAndVert(i, vertexIndex + 1);
                    vertexColors[vertexIndex + 2] = colorForLinkAndVert(i, vertexIndex + 2);
                    vertexColors[vertexIndex + 3] = colorForLinkAndVert(i, vertexIndex + 3);
                }
            }

            // Update Geometry
            pTextMeshPro.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

            return oldVertColors;
        }
    }

}
