/*
 * Copyright (c) 2011 Stephen A. Pratt
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
using UnityEngine;
using org.critterai;
using org.critterai.nav;
using org.critterai.nav.u3d;

/// <summary>
/// A centralized component used to configure and share common navigation
/// objects.
/// </summary>
/// <remarks>
/// <para>The navigation objects are initialized during the Awake operation.</para>
/// </remarks>
[System.Serializable]
[AddComponentMenu("CAI/Navigation Source")]
public class NavSource 
    : MonoBehaviour 
{
    /// <summary>
    /// The navigation mesh to be used by the query objects and
    /// <see cref="Crowd"/>.
    /// </summary>
    public BakedNavmesh navmeshSource = null;

    /// <summary>
    /// The avoidance configuration to be used by the <see cref="Crowd"/>.
    /// </summary>
    /// <remarks>
    /// <para>Required if <see cref="enableCrowdManager"/> is TRUE.</para>
    /// </remarks>
    public AvoidanceConfigSet avoidanceSource = null;

    /// <summary>
    /// The maximum search nodes to use for the query object.
    /// </summary>
    /// <remarks><para>Does not apply to the <see cref="Crowd"/>.
    /// </para></remarks>
    public int maxQueryNodes = 2048;

    /// <summary>
    /// TRUE if the <see cref="Crowd"/> should be created.
    /// </summary>
    public bool enableCrowdManager = true;

    /// <summary>
    /// The maximum number agents the <see cref="Crowd"/> will support.
    /// </summary>
    public int maxCrowdAgents = 10;

    /// <summary>
    /// The maximum agent radius the <see cref="Crowd"/> will support.
    /// </summary>
    public float maxAgentRadius = 0.5f;

    /// <summary>
    /// The initial value of <see cref="DefaultExtents"/>.
    /// </summary>
    /// <remarks>
    /// <para>Not applicable if <see cref="enableCrowdManager"/> is TRUE. In that
    /// case the <see cref="Crowd"/> extents will be used.</para>
    /// </remarks>
    public Vector3 initialExtents = new Vector3(1, 1, 1);

    [System.NonSerialized]
    private float[] mDefaultExtents = null;

    [System.NonSerialized]
    private Navmesh mNavmeshRoot = null;

    [System.NonSerialized]
    private NavmeshQuery mQueryRoot = null;

    [System.NonSerialized]
    private CrowdManager mCrowd = null;

    [System.NonSerialized]
    private NavmeshQueryFilter mDefaultFilter = null;

    [System.NonSerialized]
    private U3DNavmeshQuery mQuery = null;

    /// <summary>
    /// TRUE if the the manager's assets have been created and are ready for
    /// use.
    /// </summary>
    /// <remarks>
    /// <para>If not manually initialized ealier, the manager is initialized 
    /// during its Awake() method. So the assest are expected to be available 
    /// during the clients' Start() method.
    /// </para>
    /// </remarks>
    public bool IsActive
    {
        get
        {
            return (mQueryRoot != null
                && !mQueryRoot.IsDisposed
                && !mNavmeshRoot.IsDisposed
                && (mCrowd == null 
                    || (!mCrowd.IsDisposed && enabled)));
        }
    }

    /// <summary>
    /// The navigation mesh used by the query and <see cref="Crowd"/> objects.
    /// </summary>
    public Navmesh Navmesh { get { return mNavmeshRoot; } }

    /// <summary>
    /// The root <see cref="NavmeshQuery"/> object used by 
    /// <see cref="Query"/>.
    /// </summary>
    public NavmeshQuery QueryRoot { get { return mQueryRoot; } }

    /// <summary>
    /// The shared <see cref="U3DNavmeshQuery"/> object.
    /// </summary>
    public U3DNavmeshQuery Query { get { return mQuery; } }

    /// <summary>
    /// The shared <see cref="CrowdManager"/> object.
    /// </summary>
    public CrowdManager Crowd { get { return mCrowd; } }

    /// <summary>
    /// The shared default extents. (A reference, not a copy.)
    /// [Form: (x, y, x)]
    /// </summary>
    public float[] DefaultExtents { get { return mDefaultExtents; } }

    /// <summary>
    /// The default shared <see cref="NavmeshQueryFilter"/>.
    /// </summary>
    public NavmeshQueryFilter DefaultFilter 
    { 
        get { return mDefaultFilter; }
        set { mDefaultFilter = value; }
    }

    /// <summary>
    /// Creates all navigation objects.
    /// </summary>
    /// <remarks>
    /// <para>If active, all existing objects are disposed and new
    /// objects created.</para>
    /// </remarks>
    /// <returns>True if all objects were successfully created.</returns>
    public bool Initialize()
    {
        if (navmeshSource == null || !navmeshSource.HasNavmesh)
        {
            Debug.LogError(name + ": Aborted query creation. No navmesh.");
            return false;
        }

        Reset();

        mNavmeshRoot = navmeshSource.GetNavmesh();
        NavStatus status
            = NavmeshQuery.Build(mNavmeshRoot, maxQueryNodes, out mQueryRoot);
        if (NavUtil.Failed(status))
        {
            mNavmeshRoot = null;
            Debug.LogError(name + ": Aborted query creation: "
                + status.ToString());
            return false;
        }

        if (enableCrowdManager && avoidanceSource == null)
        {
            mNavmeshRoot = null;
            Debug.LogError(name + ": Aborted crowd manager creation."
                + " No avoidance configuration.");
            return false;
        }

        mQuery = new U3DNavmeshQuery(mQueryRoot);

        if (enableCrowdManager)
        {
            mCrowd =
                new CrowdManager(maxCrowdAgents, maxAgentRadius, mNavmeshRoot);
            mDefaultExtents = mCrowd.GetQueryExtents();
            mDefaultFilter = mCrowd.QueryFilter;
            int count = Mathf.Min(CrowdManager.MaxAvoidanceParams
                , AvoidanceConfigSet.MaxCount);
            for (int i = 0; i < count; i++)
            {
                mCrowd.SetAvoidanceConfig(i, avoidanceSource[i]);
            }
        }
        else
        {
            mCrowd = null;
            mDefaultExtents =
                Vector3Util.GetVector(initialExtents, new float[3]);
            mDefaultFilter = new NavmeshQueryFilter();
        }

        return true;
    }

    /// <summary>
    /// Disposes of all navigation objects.
    /// </summary>
    public void Reset()
    {
        if (IsActive)
        {
            mDefaultExtents = null;
            if (mCrowd != null)
                mCrowd.RequestDisposal();
            mCrowd = null;
            mDefaultFilter.RequestDisposal();
            mDefaultFilter = null;
            mQueryRoot.RequestDisposal();
            mQueryRoot = null;
            mQuery = null;
            mNavmeshRoot.RequestDisposal();
            mNavmeshRoot = null;
        }
    }

	void Awake() 
    {
        if (IsActive)
            return;
        Initialize();
	}
}