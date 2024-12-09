using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Runtime.InteropServices.WindowsRuntime;

public class Catbus : CogsAgent
{
    // ------------------BASIC MONOBEHAVIOR FUNCTIONS-------------------
    private bool majorityCollected = false;    
    private float maxCarryCapacity = 3;
    private int action = 1;
    private int previousAction = 0;
    private bool bugfix = false;
    private bool searchingNew = true;
    private Coroutine myRoutine;
    private bool wasFrozen = false;


    private string notify = "bam!";


    // Initialize values
    protected override void Start()
    {
        base.Start();
        AssignBasicRewards();
        myRoutine = StartCoroutine(EveryFiveSeconds());
    }

    // For actual actions in the environment (e.g. movement, shoot laser)
    // that is done continuously
    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        LaserControl();
        // Movement based on DirToGo and RotateDir
        moveAgent(dirToGo, rotateDir);        

        //only shoot when enemy is there 
        ShootLaser();  

        if (CountTargetsInHomeBase() >= 5) {
            majorityCollected = true;
        } else {
            if (5 - CountTargetsInHomeBase() < 3) {
                maxCarryCapacity = 5 - CountTargetsInHomeBase();
            } else {
                maxCarryCapacity = 3;
            }
            majorityCollected = false;
        }

        // restart timer when frozen
        if (IsFrozen() && myRoutine != null) {
            StopCoroutine(myRoutine);
            wasFrozen = true;
            myRoutine = StartCoroutine(EveryFiveSeconds());
            wasFrozen = false;
        }
    }

    // --------------------AGENT FUNCTIONS-------------------------

    // Get relevant information from the environment to effectively learn behavior

    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent velocity in x and z axis 
        var localVelocity = transform.InverseTransformDirection(rBody.velocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

        // Time remaining
        sensor.AddObservation(timer.GetComponent<Timer>().GetTimeRemaning());  

        // Agent's current rotation
        var localRotation = transform.rotation;
        sensor.AddObservation(transform.rotation.y);

        // Agent and home base's position and number of balls in home base
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(baseLocation.localPosition);       
        sensor.AddObservation(CountTargetsInHomeBase());

        // Enemy and enemy base's position and number of balls in enemy base
        sensor.AddObservation(enemy.transform.localPosition);
        sensor.AddObservation(GameObject.Find("Base " + enemy.GetComponent<CogsAgent>().GetTeam()).transform.localPosition);
        sensor.AddObservation(CountTargetsInEnemyBase());

        // for each target in the environment, add: its position, whether it is being carried,
        // and whether it is in a base
        foreach (GameObject target in targets){
            sensor.AddObservation(target.transform.localPosition);
            sensor.AddObservation(target.GetComponent<Target>().GetCarried());
            sensor.AddObservation(target.GetComponent<Target>().GetInBase());
        }
        
        // Whether the agent is frozen
        sensor.AddObservation(IsFrozen());
    }

    // For manual override of controls. This function will use keyboard presses to simulate output from your NN 
    public override void Heuristic(in ActionBuffers actionsOut)
{
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0; //Simulated NN output 0
        discreteActionsOut[1] = 0; //....................1
        discreteActionsOut[2] = 0; //....................2
        discreteActionsOut[3] = 0; //....................3

        discreteActionsOut[4] = 0;
       
        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActionsOut[0] = 1;
        }       
        if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[1] = 1;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActionsOut[1] = 2;            
        }
        
        //Shoot
        if (Input.GetKey(KeyCode.Space)){
            discreteActionsOut[2] = 1;
        }

        //GoToNearestTarget
        if (Input.GetKey(KeyCode.A)){
            discreteActionsOut[3] = 1;
        }

        //GoToBase
        if (Input.GetKey(KeyCode.B)){
            discreteActionsOut[4] = 1;
        }

        //debug action
        if (Input.GetKey(KeyCode.R)) {            
            Debug.Log(notify);
        }

    }

    // What to do when an action is received (i.e. when the Brain gives the agent information about possible actions)
    public override void OnActionReceived(ActionBuffers actions) {
        if (!bugfix) {
            ComputeAction();
        }        

        switch (action) {
            case 0:
                // do nothing
                break;
            case 1:
                notify = "shooting enemy because it is close and carrying something";
                MovePlayer(0,0,0,0,0,0,1,0);
                break;
            case 2:
                notify = "shooting enemy because it is carrying more than 2";
                MovePlayer(0,0,0,0,0,0,1,0);
                break;
            case 3:
                notify = "going back to base";
                MovePlayer(0,0,0,0,0,1,0,0);
                break;
            case 4:
                notify = "looking for target";
                MovePlayer(0,0,0,1,0,0,0,0);
                break;
            case 5:
                notify = "shooting enemy after collecting majority";
                MovePlayer(0,0,0,0,0,0,1,0);
                break;
            case 6:
                notify = "returning to base to obtain majority";
                MovePlayer(0,0,0,0,0,1,0,0);
                break;
            case 7:
                notify = "collecting closest to base after collecting majority";
                MovePlayer(0,0,0,0,1,0,0,0);
                break;
            case 8:
                notify = "heading back to base to protect";
                MovePlayer(0,0,0,0,0,0,0,1);                
                break;
            case 9:
                notify = "collecting closest target to base";
                MovePlayer(0,0,0,0,1,0,0,0);
                break;
        }
    }

// ----------------------ONTRIGGER AND ONCOLLISION FUNCTIONS------------------------
    // Called when object collides with or trigger (similar to collide but without physics) other objects
    protected override void OnTriggerEnter(Collider collision) {                
        base.OnTriggerEnter(collision);
    }

    protected override void OnCollisionEnter(Collision collision) 
    {       
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() == 0 && collision.gameObject.GetComponent<Target>().GetCarried() == 0 && !IsFrozen())
        {
            searchingNew = true;
        }
        base.OnCollisionEnter(collision);
    }
    //  --------------------------HELPERS---------------------------- 
     private void AssignBasicRewards() {
        rewardDict = new Dictionary<string, float>();

        rewardDict.Add("frozen", -0.1f);
        rewardDict.Add("shooting-laser", 0.1f);
        rewardDict.Add("hit-enemy", 0.5f);
        rewardDict.Add("dropped-one-target", -0.1f);
        rewardDict.Add("dropped-targets", -0.5f);
    }
    
    private void MovePlayer(int forwardAxis, int rotateAxis, int shootAxis, int goToTargetAxis, int GoToNearestTargetBaseAxis, int goToBaseAxis, int goToEnemyAxis, int defendBaseAxis) {
        dirToGo = Vector3.zero;
        rotateDir = Vector3.zero;

        Vector3 forward = transform.forward;
        Vector3 backward = -transform.forward;
        Vector3 right = transform.up;
        Vector3 left = -transform.up;

        //forwardAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (forwardAxis == 0){
            //do nothing. This case is not necessary to include, it's only here to explicitly show what happens in case 0
        }
        else if (forwardAxis == 1){
            dirToGo = forward;
        }
        else if (forwardAxis == 2){
            dirToGo = backward;            
        }

        //rotateAxis: 
            // 0 -> do nothing
            // 1 -> go right
            // 2 -> go left
        if (rotateAxis == 0){
            //do nothing
        }
        else if (rotateAxis == 1) {
            rotateDir = right;
        }
        else if (rotateAxis == 2) {
            rotateDir = left;
        }        

        //go to the nearest target
        if (goToTargetAxis == 1) {
            GoToNearestTarget();
        }

        //go to nearest target to base
        if (GoToNearestTargetBaseAxis == 1) {
            GoToNearestTargetToBase();
        }

        if (goToBaseAxis == 1){
            GoToBase();
        }

        if (goToEnemyAxis == 1){
            GoToEnemy();
        }

        if (defendBaseAxis == 1){
            DefendBase();
        }

        // shoot (handled in ShootLaser())
        // if (shootAxis == 1) {
        //     SetLaser(true);
        // }
        // else {
        //     SetLaser(false);
        // }
        
    }

    // compute which action should be taken
    private void ComputeAction() {
        if (!majorityCollected) {
            if (GetDistanceToEnemy() <= 20 && GetCarrying() <= maxCarryCapacity && enemy.GetComponent<CogsAgent>().GetCarrying() > 0)
            {
                action = 1;                
            } else if (CountTargetsInHomeBase() + GetCarrying() >= 5 || (GetCarrying() > 0 && timer.GetComponent<Timer>().GetTimeRemaning() < 60)) {
                action = 3;
            } else if (enemy.GetComponent<CogsAgent>().GetCarrying() >= 2 && GetCarrying() < 2) {
                action = 2;
            } else if (GetCarrying() >= maxCarryCapacity || (DistanceToBase() < GetDistanceToNearestTarget() && GetCarrying() > 0)) {
                action = 3;
            } else if (timer.GetComponent<Timer>().GetTimeRemaning() < 60) {
                action = 9;
            } else if (GetCarrying() < maxCarryCapacity) {
                action = 4;
            }
        } else {
            if (enemy.GetComponent<CogsAgent>().GetCarrying() > 0) {
                action = 5;
            } else if (GetDistanceToNearestTarget() < DistanceToBase() && GetCarrying() < maxCarryCapacity && DistanceToBase() > 50) {
                action = 7;
            } else if (GetDistanceToNearestTarget() < DistanceToBase() && GetCarrying() < 1) {
                action = 7;
            }  else if (GetCarrying() > 0) {
                action = 3;
            } else if (DistanceToBase() >= 5) {
                action = 8;
            }           
        }
    }

    // Go to home base
    private void GoToBase() {
        TurnAndGo(GetYAngle(myBase));
    }

    // go to enemy position
    private void GoToEnemy() {
        TurnAndGo(GetYAngle(enemy));
    }

    // return to base and face in enemy direction
    private void DefendBase() {
        Vector3 defendPosition = Vector3.zero;

        if (GetTeam() == 1) {
            defendPosition = new Vector3(20f,0.5f,20f);
        } else {
            defendPosition = new Vector3(-20f,0.5f,-20f);
        }

        if (Vector3.Distance(transform.localPosition,defendPosition) >= 3) {
            TurnAndGo(GetYAngleVector(defendPosition));
        } else {
            dirToGo = Vector3.zero;
            Turn(GetYAngle(enemy));
        }
    }


    // Go to the nearest target
    private void GoToNearestTarget() {
        GameObject target = GetNearestTarget();
        if (target != null){
            float rotation = GetYAngle(target);
            TurnAndGo(rotation);
        }        
    }

    // Go to the nearest target to base
    private void GoToNearestTargetToBase() {
        GameObject target = GetNearestTargetToBase();
        if (target != null){
            float rotation = GetYAngle(target);
            TurnAndGo(rotation);
        }     
    }

    // Rotate and go in specified direction
    private void TurnAndGo(float rotation) {

        if(rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
        else {
            dirToGo = transform.forward;
        }
    }

    // rotate to face specified direction
    private void Turn(float rotation) {
        if(rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
    }

    // return reference to nearest target
    protected GameObject GetNearestTarget() {
        float distance = 200;
        GameObject nearestTarget = null;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team)
            {
                distance = currentDistance;
                nearestTarget = target;
            }
        }
        
        return nearestTarget;
    }

    // return reference to nearest target to base
    protected GameObject GetNearestTargetToBase() {
        float distance = 200;
        GameObject nearestTarget = null;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, myBase.transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team)
            {
                distance = currentDistance;
                nearestTarget = target;
            }
        }
        return nearestTarget;
    }
    
    // return distance to nearest target
    private float GetDistanceToNearestTarget() {
        float distance = 400;
        float distanceToNearest = 400;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team)
            {
                distance = currentDistance;
                distanceToNearest = currentDistance;
            }
        }
        
        return distanceToNearest;
    }

    // return distance to enemy
    private float GetDistanceToEnemy() {
        return Vector3.Distance(transform.localPosition, enemy.transform.localPosition);
    }
    private float GetYAngle(GameObject target) {
        
       Vector3 targetDir = target.transform.localPosition - transform.localPosition;
       Vector3 forward = transform.forward;

      float angle = Vector3.SignedAngle(targetDir, forward, Vector3.up);
      return angle;         
    }

    // get y angle of a given vector position
    private float GetYAngleVector(Vector3 vector) {
        Vector3 targetDir = vector - transform.localPosition;
        Vector3 forward = transform.forward;

        float angle = Vector3.SignedAngle(targetDir,forward,Vector3.up);
        return angle;
    }

    //shoot laser only when the enemy is seen    
    private void ShootLaser() {
        RaycastHit hit;
        if (Physics.Raycast(transform.localPosition, transform.forward, out hit, 20) && hit.collider.CompareTag("Player"))
        {
            if (hit.collider.GetComponent<CogsAgent>().GetTeam() == enemy.GetComponent<CogsAgent>().GetTeam()) {
                SetLaser(true);
            } 
        } else {
            SetLaser(false);
        }
    }

    //count targets in home base
    private int CountTargetsInHomeBase() {
        int targetsInBase = 0;

        foreach (GameObject target in targets) {
            if (target.GetComponent<Target>().GetInBase() == team) {
                targetsInBase++;
            }
        }
        return targetsInBase; 
    }

    //count targets in enemy base
    private int CountTargetsInEnemyBase() {
        int targetsInBase = 0;
        int enemyTeam = enemy.GetComponent<CogsAgent>().GetTeam();

        foreach (GameObject target in targets) {
            if (target.GetComponent<Target>().GetInBase() == enemyTeam) {
                targetsInBase++;
            }
        }
        return targetsInBase; 
    }

    //every five seconds, check if bot is stuck (action has not changed when it is supposed to)
    private IEnumerator EveryFiveSeconds() {
        while (true) {
            if (action == previousAction && action != 3 && action != 8 && !searchingNew && !wasFrozen) {
                previousAction = action;
                if (majorityCollected) {
                    action = 8;
                } else {
                    action = 3;
                }                
                bugfix = true;                
                yield return new WaitForSeconds(5f);
            } else {
                previousAction = action;
                bugfix = false;         
                searchingNew = false;   
                yield return new WaitForSeconds(5f);
            }
            
        }
    }
}
