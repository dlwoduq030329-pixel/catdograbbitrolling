using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class EnemySkill : EnemySkillBase
{
    [SerializeField]
    public List<string> enemySkill;
    [SerializeField]
    Transform[] poses;
    [SerializeField]
    Transform[] scarecrow;

    EnemyStateMachine esm;
    [SerializeField]
    GameObject tempTarget;
    [SerializeField]
    Material transparent;
    int posIndex = 0;
    Animator anim;
    [SerializeField]
    GameObject scarePrefab;
    GameObject hitBox;
    List<GameObject> enemies = new List<GameObject>();

    HashSet<UnitState> hitTargets = new();

    bool skillUsing = false;
    public bool SkillUsing => skillUsing;
    private void Awake()
    {
         anim = GetComponent<Animator>();
        tempTarget = BattleManager.Instance.Player;
        esm = GetComponent<EnemyStateMachine>();
    }

    public Collider[] DrawBoxCollider(Vector3 pos, float range,float width,float height)
    {
        tempTarget = GetComponent<EnemyStateMachine>().Target;
        int layerMask = LayerMask.GetMask("Player");
        //(esm.Target.transform.position
        


        Vector3 dir = (tempTarget.transform.position - pos).normalized;
        //dir.z = 0;
        dir.y = 0;
        Quaternion rot = Quaternion.LookRotation(dir);

        Vector3 center =  transform.position + dir * (range * 0.5f);


        Collider[] hits = Physics.OverlapBox(
            center,
            new Vector3(width * 0.5f, height * 0.5f, range * 0.5f),
            rot,
            LayerMask.GetMask("Player"));


        return hits;
    }

    public void InitPos(Transform[] scarePos, Transform[] enemiesPos)
    {
        scarecrow = scarePos;
        poses = enemiesPos;
    }

    int skillCount;
    // Start is called before the first frame update
    void Start()
    {
        //skillCount = enemySkill.Count;
        //Invoke(nameof(UseSkill), 0.5f);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public override void UseSkill()
    {
        int x = Random.Range(0, 2);

        //x = 1;
        skillUsing = true;
        anim.Play(enemySkill[x].ToString());
        StartCoroutine(spawnScare());
        base.UseSkill();
    }

    public void slowAnim()
    {
        anim.speed = 0.4f;
    }

    public void fastAnim()
    {
        anim.speed = 1.5f;
    }

    public void normalSpeed()
    {
        anim.speed = 1;
    }

    public void DrawDashBound()
    {
        Vector3 dir = (tempTarget.transform.position - this.transform.position).normalized;
        //dir.z = 0;
        dir.y = 0;
        Quaternion rot = Quaternion.LookRotation(dir);

        Vector3 center = transform.position + dir * (1 * 0.5f);

        hitBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(hitBox.GetComponent<BoxCollider>());
        Collider col = hitBox.GetComponent<Collider>();

        if (col != null)
        {
            col.enabled = false;
        }
        hitBox.GetComponent<MeshRenderer>().material = transparent;

        hitBox.transform.position = center;
        hitBox.transform.localScale = new Vector3(200,3,1);

        Invoke("DelBound", 2f);

    }

    public void DelBound()
    {
        Destroy(hitBox);

    }

    public IEnumerator spawnScare()
    {
        enemies.Clear();
        for(int i =0; i< scarecrow.Length;i++)
        {
            var temp = Instantiate(scarePrefab, scarecrow[i].position, Quaternion.identity);
            temp.GetComponent<Scareclow>().Init(9999f, true);
            temp.GetComponent<Scareclow>().DelSet();
            enemies.Add(temp);
            BattleManager.Instance.AddEnemy(temp);
        }
        yield return new WaitForSeconds(5f);
        
    }


    public void DrawSwingBound()
    {
        Vector3 dir = (tempTarget.transform.position - this.transform.position).normalized;
        //dir.z = 0;
        dir.y = 0;
        Quaternion rot = Quaternion.LookRotation(dir);

        Vector3 center = transform.position + dir * (1 * 0.5f);

        hitBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(hitBox.GetComponent<BoxCollider>());
        Collider col = hitBox.GetComponent<Collider>();

        if (col != null)
        {
            col.enabled = false;
        }
        hitBox.GetComponent<MeshRenderer>().material = transparent;

        hitBox.transform.position = center;
        hitBox.transform.localScale = new Vector3(4, 0.1f,7);
        Invoke("DelBound", 2f);

    }

    public void EndSkill()
    {
        Debug.Log("스킬 끝!");
        skillUsing = false;

       
            Debug.Log("원래 상태로!");
            esm.ChangePlayerState(esm.BeforeST);
    }
    

    public void Swing()
    {
        Collider[] target = DrawBoxCollider(this.transform.position,7 ,4, 1);


        foreach (Collider col in target)
        {
            UnitState targetst = col.gameObject.GetComponent<UnitState>();
            targetst.GetDam(10f);
            targetst.CrowdControl(CC.Stun, 3f);
        }

        Invoke("EndSkill", 2.9f);
    }

    public void Dash()
    {
     
        StartCoroutine(DashAnim());


    }

    public IEnumerator DashAnim()
    {
        hitTargets.Clear();
        anim.Play("Dash1");
        DrawDashBound();
        this.transform.position = poses[posIndex].position;
        posIndex = (posIndex + 1) % 2;

        Vector3 tempPos = poses[posIndex].position;
        tempPos.y = this.transform.position.y;

        float distance = Vector3.Distance(this.transform.position, poses[posIndex].position);
        
        while (distance >0.001f)
        {
            distance = Vector3.Distance(this.transform.position, poses[posIndex].position);
            this.gameObject.transform.LookAt(tempPos);

            this.transform.position = Vector3.MoveTowards(this.transform.position, poses[posIndex].position, 20 * Time.deltaTime);

            Collider[] target = DrawBoxCollider(this.transform.position, 3, 10, 1);

            foreach (Collider col in target)
            {
                UnitState us = col.GetComponent<UnitState>();

                if(us!= null && hitTargets.Add(us))
                {
                    us.GetDam(10f);

                }

            }


            yield return null;
        }
        

        yield return new WaitForSeconds(1f);
        EndSkill();


    }


    private void OnDisable()
    {
     

    }

}
