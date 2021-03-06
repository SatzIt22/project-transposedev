using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

public class AIScript : MonoBehaviourPunCallbacks, IDamageable
{
    
    public NavMeshAgent agent; //reference to agent
    public Transform player;
    public LayerMask GroundSensor;
    public LayerMask PlayerSensor;
    public LayerMask WallSensor;
    public float health;
  
    public Vector3 walkpoint;
    bool walkpointSet;
    public float walkpointRange;
  
    public float attackCooldown;
    bool alreadyAttacked;
  
    public float sightRange;
    public float attackRange;
    public bool playerInSightRange;
    public bool playerInAttackRange;
    public bool playerInSight;

    // items that can be held by the bot
    [SerializeField] Item[] items;
    int itemIndex;
    int previousItemIndex = -1;

    PhotonView PV;

    public string id;
    public int kills = 0;
    public int deaths = 0;

    private void Awake()
    {
        PV = GetComponent<PhotonView>();
        agent = GetComponent<NavMeshAgent>();
        id = "bot" + PV.ViewID;
    }

    void Start()
    {
        EquipItem(0);
        Invoke(nameof(SetPlayer), 3);
    }

    private void Update()
    {
        if (!PV.IsMine)
            return;

        foreach (PlayerMovement p in FindObjectsOfType<PlayerMovement>()) //Designate nearest target
        {
            float minDistance = float.MaxValue;
            if(Vector3.Distance(transform.position, p.transform.position) < minDistance)
            {
                //Debug.Log("Selected Target");
                player = p.transform;
            }
        }

      playerInSightRange = Physics.CheckSphere(transform.position, sightRange, PlayerSensor);
      playerInAttackRange = Physics.CheckSphere(transform.position, attackRange, PlayerSensor);

        RaycastHit hit;
        if(Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity))
        {
            //Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);

            if (hit.transform.GetComponentInParent<PlayerMovement>() != null) {
                playerInSight = true;
            }
            else {
                playerInSight = false;
            }

            //Debug.Log(hit.transform.name + " AHHHHHHHHHHHH it sees that object");
        }
        
      if (playerInSightRange && playerInAttackRange)
      {
            AttackMode();
      }
      
        if (!playerInSightRange && !playerInAttackRange)
        {
            PatrolMode();
        }

        if (playerInSightRange && !playerInAttackRange)
        {
            ChaseMode();
        }
    }   
 
    private void AttackMode()
    {
        //Debug.Log("Entering Attack Mode");
        agent.SetDestination(transform.position);
        transform.LookAt(player);

        if (playerInSight) { // if AI is staring at PlayerController
            //for fully auto guns
            items[itemIndex].HoldDown();
            if (!alreadyAttacked) { //for single shot
                items[itemIndex].Use();
                alreadyAttacked = true;
                Invoke(nameof(ResetAttack), attackCooldown);
            }
        } else { // There's something in the way
            if (player != null)
                agent.SetDestination(player.position); // Go find player
        }
    }

    void EquipItem(int index)
    {
        if (index == previousItemIndex)
            return;

        itemIndex = index;

        items[itemIndex].itemGameObject.SetActive(true);

        if (previousItemIndex != -1)
        {
            items[previousItemIndex].itemGameObject.SetActive(false);
        }

        previousItemIndex = itemIndex;

        /*
        // I leave it here in case you will need it
        if (PV.IsMine)
        {
            hash = PhotonNetwork.LocalPlayer.CustomProperties;
            hash.Remove("itemIndex");
            hash.Add("itemIndex", itemIndex);
            PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
        }
        */
    }

    private void PatrolMode()
    {
        //Debug.Log("Entering Patrol Mode");
        if (walkpointSet != true)
        {
            SearchWalkpoint();
        }
        else
            agent.SetDestination(walkpoint);
   
        Vector3 distToWalkpoint = transform.position - walkpoint;
        if(distToWalkpoint.magnitude < 1f)
        {
             walkpointSet = false;
        }
        //after a certain amount of time in patrol mode, change weapon
    }
   
    private void ChaseMode()
    {
        //Debug.Log("Entering Chase Mode");
        if (player == null)
            return;
        agent.SetDestination(player.position);
        //if bot is in chase mode after a certain amount of time{
        //change weapon to longer range weapon if not equipped already}
    }  
 
    private void ResetAttack()
    {
        alreadyAttacked = false;
    } 
  
    private void SearchWalkpoint()
    {
        // Debug.Log("Searching for walkpoint");
        float randomX = Random.Range(-walkpointRange, walkpointRange);
        float randomZ = Random.Range(-walkpointRange, walkpointRange);
        
        walkpoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);
        
        //checks if walkpoint is on map
        if (Physics.Raycast(walkpoint, -transform.up, 2f, GroundSensor))
        {
            walkpointSet = true;
        }
        //Debug.Log("walkpoint is " + walkpoint.ToString());
    }
  
    private void killEnemy()
    {
        Destroy(gameObject);
    }
    
    //public void changeWeapon(int weapon){
    //    if (!playerInSightRange && !playerInAttackRange){//AI can't change weapons in chase or attack mode
    //          EquipItem(weapon);
    //    }
    //}    
  
    //uncomment to make sightRange & attackRange visible in-game
    private void makeSightRangesVisible() 
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }

    private void SetPlayer()
    {
        player = GameObject.FindGameObjectsWithTag("Player")[0].transform;
    }

    public void TakeDamage(float damage, Component source)
    {
        // find player owner of gun
        if (source is Gun && (source.GetComponentInParent<PlayerMovement>() != null || source.GetComponentInParent<PlayerMovement_Grappler>() != null))
            PV.RPC("RPC_TakeDamage", RpcTarget.All, damage, PhotonNetwork.LocalPlayer, null);
        // find ai owner of gun
        if (source is Gun && source.GetComponentInParent<AIScript>() != null)
            PV.RPC("RPC_TakeDamage", RpcTarget.All, damage, null, source.GetComponentInParent<AIScript>().GetId());
        // find player or ai that blew up barrel
        if (source is ExplosiveBarrel)
            PV.RPC("RPC_TakeDamage", RpcTarget.All, damage, null, null);
        // find player owner of rocket
        if (source is RocketBehaviour)
            PV.RPC("RPC_TakeDamage", RpcTarget.All, damage, PhotonNetwork.LocalPlayer, null);
        if (source is GrenadeBehaviour)
            PV.RPC("RPC_TakeDamage", RpcTarget.All, damage, PhotonNetwork.LocalPlayer, null);
    }

    [PunRPC]
    void RPC_TakeDamage(float damage, Player shooter, string botId)
    {
        health -= damage;

        if (health <= 0)
        {
            if (shooter != null)
			{
                if (PhotonNetwork.IsMasterClient)
                    GameManager.Instance.UpdatePlayerKills(shooter);
			}
            // destroy the game object taking damage (kill the AI player)...
            Die();
        }
    }

    public void Die()
	{
        gameObject.SetActive(false);
        Invoke("Respawn", 3);

        if (!PhotonNetwork.IsMasterClient)
            return;

        Hashtable hash = PhotonNetwork.MasterClient.CustomProperties;
        string key = id + "_deaths";
        int deaths = (int)hash[key] + 1;
        hash.Remove(key);
        hash.Add(key, deaths);
        PhotonNetwork.MasterClient.SetCustomProperties(hash);
    }

    public void Respawn()
	{
        Transform spawnPoint = SpawnManager.Instance.GetSpawnPoint();
        if (PhotonNetwork.IsMasterClient)
        {
            gameObject.transform.position = spawnPoint.position;
            gameObject.transform.rotation = spawnPoint.rotation;
        }
        gameObject.SetActive(true);
	}

    public override void OnEnable()
    {
        base.OnEnable();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        health = 100;
    }

    public string GetId() { return id; }

    public int GetKills() { return kills; }

    public int GetDeaths() { return deaths; }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
		try
		{
            kills = (int)PhotonNetwork.MasterClient.CustomProperties[id + "_kills"];
            deaths = (int)PhotonNetwork.MasterClient.CustomProperties[id + "_deaths"];
        }
        catch (System.Exception e)
		{
            Debug.Log(e);
		}
    }
}
