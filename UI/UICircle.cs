using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class UICircle : MaskableGraphic
{
    [SerializeField]
    Texture m_Texture;

    [Range(0, 1)]
    [SerializeField]
    private float fillAmount;

    public float FillAmount
    {
        get { return fillAmount; }
        set
        {
            fillAmount = value;

            // This detects a change and sets the vertices dirty so it gets updated
            SetVerticesDirty();
        }
    }

    public bool fill = true;
    public int thickness = 5;

    [Range(0, 360)]
    public int segments = 360;

    public override Texture mainTexture
    {
        get
        {
            return m_Texture == null ? s_WhiteTexture : m_Texture;
        }
    }

    //float oldFillAmount;

    /// <summary>
    /// Texture to be used.
    /// </summary>
    public Texture texture
    {
        get { return m_Texture; }

        set
        {
            if (m_Texture == value)
                return;
            m_Texture = value;
            SetVerticesDirty();
            SetMaterialDirty();
        }
    }

    // Using arrays is a bit more efficient
    UIVertex[] uiVertices = new UIVertex[4];
    Vector2[] uvs = new Vector2[4];
    Vector2[] pos = new Vector2[4];

    protected override void Start()
    {
        //oldFillAmount = fillAmount;

        uvs[0] = new Vector2(0, 1);
        uvs[1] = new Vector2(1, 1);
        uvs[2] = new Vector2(1, 0);
        uvs[3] = new Vector2(0, 0);
    }

    // Removed Update because it's more efficient to use a property
    //void Update()
    //{
    //if (!Mathf.Approximately(oldFillAmount, fillAmount))
    //{
    //    SetVerticesDirty();
    //    oldFillAmount = fillAmount;
    //}
    //}


    // Updated OnPopulateMesh to user VertexHelper instead of mesh
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        // There's really no need to clamp the thickness
        //thickness = (int)Mathf.Clamp(thickness, 0, (rectTransform.rect.width / 2));
        float outer = -rectTransform.pivot.x * rectTransform.rect.width;
        float inner = -rectTransform.pivot.x * rectTransform.rect.width + thickness;

        float degrees = 360f / segments;
        int fa = (int)((segments + 1) * this.fillAmount);

        // Updated to new vertexhelper
        vh.Clear();

        //toFill.Clear();
        //var vbo = new VertexHelper(toFill);
        //UIVertex vert = UIVertex.simpleVert;

        // Changed initial values so the first polygon is correct when circle isn't filled
        float x = outer * Mathf.Cos(0);
        float y = outer * Mathf.Sin(0);
        Vector2 prevX = new Vector2(x, y);

        // Changed initial values so the first polygon is correct when circle isn't filled
        x = inner * Mathf.Cos(0);
        y = inner * Mathf.Sin(0);
        Vector2 prevY = new Vector2(x, y);

        for (int i = 0; i < fa - 1; i++)
        {
            // Changed so there isn't a stray polygon at the beginning of the arc
            float rad = Mathf.Deg2Rad * ((i + 1) * degrees);
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            //float x = outer * c;
            //float y = inner * c;

            pos[0] = prevX;
            pos[1] = new Vector2(outer * c, outer * s);

            if (fill)
            {
                pos[2] = Vector2.zero;
                pos[3] = Vector2.zero;
            }
            else
            {
                pos[2] = new Vector2(inner * c, inner * s);
                pos[3] = prevY;
            }

            // Set values for uiVertices
            for (int j = 0; j < 4; j++)
            {
                uiVertices[j].color = color;
                uiVertices[j].position = pos[j];
                uiVertices[j].uv0 = uvs[j];
            }

            // Get the current vertex count
            int vCount = vh.currentVertCount;

            // If filled, we only need to create one triangle
            vh.AddVert(uiVertices[0]);
            vh.AddVert(uiVertices[1]);
            vh.AddVert(uiVertices[2]);

            // Create triangle from added vertices
            vh.AddTriangle(vCount, vCount + 2, vCount + 1);

            // If not filled we need to add the 4th vertex and another triangle
            if (!fill)
            {
                vh.AddVert(uiVertices[3]);
                vh.AddTriangle(vCount, vCount + 3, vCount + 2);
            }

            prevX = pos[1];
            prevY = pos[2];

            // Removed so we can just use a single triangle when not filled
            //vh.AddUIVertexQuad(SetVbo(new[] { pos0, pos1, pos2, pos3 }, new[] { uv0, uv1, uv2, uv3 }));
        }

        // Removed because we don't need it any more with new OnPopulateMesh using a VertexHelper instead of a Mesh
        //if (vbo.currentVertCount > 3)
        //{
        //    vbo.FillMesh(toFill);
        //}
    }
}