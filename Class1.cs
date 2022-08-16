using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace RedQueen
{
    public class BeamModule : ItemModule
    {
        public Color BeamColor;
        public Color BeamEmission;
        public Vector3 BeamSize;
        public float BeamSpeed;
        public float DespawnTime;
        public float BeamDamage;
        public bool BeamDismember;
        public Vector3 BeamScaleIncrease;
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<BeamCustomization>().Setup(BeamDismember, BeamSpeed, DespawnTime, BeamDamage, BeamColor, BeamEmission, BeamSize, BeamScaleIncrease);
        }
    }
    public class BeamCustomization : MonoBehaviour
    {
        Item item;
        public Color beamColor;
        public Color beamEmission;
        public Vector3 beamSize;
        float despawnTime;
        float beamSpeed;
        float beamDamage;
        bool dismember;
        Vector3 beamScaleUpdate;
        List<RagdollPart> parts = new List<RagdollPart>();
        public void Start()
        {
            item = GetComponent<Item>();
            item.renderers[0].material.SetColor("_BaseColor", beamColor);
            item.renderers[0].material.SetColor("_EmissionColor", beamEmission * 2f);
            item.renderers[0].gameObject.transform.localScale = beamSize;
            item.rb.useGravity = false;
            item.rb.drag = 0;
            item.rb.AddForce(Player.local.head.transform.forward * beamSpeed, ForceMode.Impulse);
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
            item.RefreshCollision(true);
            item.Throw();
            item.Despawn(despawnTime);
        }
        public void Setup(bool beamDismember, float BeamSpeed, float BeamDespawn, float BeamDamage, Color color, Color emission, Vector3 size, Vector3 scaleUpdate)
        {
            dismember = beamDismember;
            beamSpeed = BeamSpeed;
            despawnTime = BeamDespawn;
            beamDamage = BeamDamage;
            beamColor = color;
            beamEmission = emission;
            beamSize = size;
            beamScaleUpdate = scaleUpdate;
        }
        public void FixedUpdate()
        {
            item.gameObject.transform.localScale += beamScaleUpdate;
        }
        public void Update()
        {
            if (parts.Count > 0)
            {
                parts[0].gameObject.SetActive(true);
                parts[0].bone.animationJoint.gameObject.SetActive(true);
                parts[0].ragdoll.TrySlice(parts[0]);
                if (parts[0].data.sliceForceKill)
                    parts[0].ragdoll.creature.Kill();
                parts.RemoveAt(0);
            }
        }
        public void OnTriggerEnter(Collider c)
        {
            if (c.GetComponentInParent<ColliderGroup>() != null)
            {
                ColliderGroup enemy = c.GetComponentInParent<ColliderGroup>();
                if (enemy?.collisionHandler?.ragdollPart != null && enemy?.collisionHandler?.ragdollPart?.ragdoll?.creature != Player.currentCreature)
                {
                    RagdollPart part = enemy.collisionHandler.ragdollPart;
                    if (part.ragdoll.creature != Player.currentCreature && part?.ragdoll?.creature?.gameObject?.activeSelf == true && part != null && !part.isSliced)
                    {
                        if (part.sliceAllowed && dismember)
                        {
                            if (!parts.Contains(part))
                                parts.Add(part);
                        }
                        else if (!part.ragdoll.creature.isKilled)
                        {
                            CollisionInstance instance = new CollisionInstance(new DamageStruct(DamageType.Slash, beamDamage));
                            instance.damageStruct.hitRagdollPart = part;
                            part.ragdoll.creature.Damage(instance);
                            part.ragdoll.creature.TryPush(Creature.PushType.Hit, item.rb.velocity, 1);
                        }
                    }
                }
            }
        }
    }
    public class RedQueenModule : ItemModule
    {
        public float DashSpeed;
        public string DashDirection;
        public bool DisableGravity;
        public bool DisableCollision;
        public float DashTime;
        public float BeamCooldown;
        public float SwordSpeed;
        public bool StopOnEnd = false;
        public bool StopOnStart = false;
        public bool ThumbstickDash = false;
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<RedQueenComponent>().Setup(DashSpeed, DashDirection, DisableGravity, DisableCollision, DashTime, SwordSpeed, BeamCooldown, StopOnEnd, StopOnStart, ThumbstickDash); ;
        }
    }
    public class RedQueenComponent : MonoBehaviour
    {
        Item item;
        HingeJoint joint;
        bool isRevved = false;
        SpellCastCharge spell;
        bool beam;
        public bool StopOnEnd;
        public bool StopOnStart;
        bool ThumbstickDash;
        bool fallDamage;
        bool dashing;
        public float DashSpeed;
        public string DashDirection;
        public bool DisableGravity;
        public bool DisableCollision;
        public float DashTime;
        float cdH;
        float cooldown;
        float swordSpeed;
        public void Start()
        {
            item = GetComponent<Item>();
            joint = item.GetCustomReference("HandleJoint").GetComponent<HingeJoint>();
            item.OnHeldActionEvent += Item_OnHeldActionEvent;
            item.OnUngrabEvent += Item_OnUngrabEvent;
            spell = Catalog.GetData<SpellCastCharge>("Fire");
            item.mainCollisionHandler.OnCollisionStartEvent += MainCollisionHandler_OnCollisionStartEvent;
        }

        private void MainCollisionHandler_OnCollisionStartEvent(CollisionInstance collisionInstance)
        {
            if(collisionInstance.sourceColliderGroup.collisionHandler == item.mainCollisionHandler && item.colliderGroups[0].imbue.energy >= 0 && collisionInstance.targetCollider.GetComponentInParent<Creature>() != null)
            {
                item.colliderGroups[0].imbue.ConsumeInstant(35f);
            }
        }

        public void Setup(float speed, string direction, bool gravity, bool collision, float time, float SwordSpeed, float BeamCooldown, bool stop, bool start, bool thumbstick)
        {
            DashSpeed = speed;
            DashDirection = direction;
            DisableGravity = gravity;
            DisableCollision = collision;
            DashTime = time;
            if (direction.ToLower().Contains("player") || direction.ToLower().Contains("head") || direction.ToLower().Contains("sight"))
            {
                DashDirection = "Player";
            }
            else if (direction.ToLower().Contains("item") || direction.ToLower().Contains("sheath") || direction.ToLower().Contains("flyref") || direction.ToLower().Contains("weapon"))
            {
                DashDirection = "Item";
            }
            swordSpeed = SwordSpeed;
            cooldown = BeamCooldown;
            StopOnEnd = stop;
            StopOnStart = start;
            ThumbstickDash = thumbstick;
        }
        private void Item_OnUngrabEvent(Handle handle, RagdollHand ragdollHand, bool throwing)
        {
            beam = false;
        }

        private void Item_OnHeldActionEvent(RagdollHand ragdollHand, Handle handle, Interactable.Action action)
        {
            if (action == Interactable.Action.AlternateUseStart && !ragdollHand.playerHand.controlHand.usePressed)
            {
                StopCoroutine(Dash());
                StartCoroutine(Dash());
            }
            if (action == Interactable.Action.UseStart)
            {
                beam = true;
            }
            if (action == Interactable.Action.UseStop)
            {
                beam = false;
            }
        }
        public IEnumerator Dash()
        {
            dashing = true;
            Player.fallDamage = false;
            if (StopOnStart) Player.local.locomotion.rb.velocity = Vector3.zero;
            if (Player.local.locomotion.moveDirection.magnitude <= 0 || !ThumbstickDash)
                if (DashDirection == "Item")
                {
                    Player.local.locomotion.rb.AddForce(item.mainHandler.grip.up * DashSpeed, ForceMode.Impulse);
                }
                else
                {
                    Player.local.locomotion.rb.AddForce(Player.local.head.transform.forward * DashSpeed, ForceMode.Impulse);
                }
            else
            {
                Player.local.locomotion.rb.AddForce(Player.local.locomotion.moveDirection.normalized * DashSpeed, ForceMode.Impulse);
            }
            if (DisableGravity)
                Player.local.locomotion.rb.useGravity = false;
            if (DisableCollision)
            {
                Player.local.locomotion.rb.detectCollisions = false;
                item.rb.detectCollisions = false;
                item.mainHandler.rb.detectCollisions = false;
                item.mainHandler.otherHand.rb.detectCollisions = false;
            }
            yield return new WaitForSeconds(DashTime);
            if (DisableGravity)
                Player.local.locomotion.rb.useGravity = true;
            if (DisableCollision)
            {
                Player.local.locomotion.rb.detectCollisions = true;
                item.rb.detectCollisions = true;
                item.mainHandler.rb.detectCollisions = true;
                item.mainHandler.otherHand.rb.detectCollisions = true;
            }
            if (StopOnEnd) Player.local.locomotion.rb.velocity = Vector3.zero;
            Player.fallDamage = fallDamage;
            dashing = false;
            yield break;
        }

        public void Update()
        {
            if(!isRevved && (joint.angle >= 45 || joint.angle <= -45))
            {
                item.colliderGroups[0].imbue.Transfer(spell, 35);
                EffectInstance instance = Catalog.GetData<EffectData>("RedQueenRev").Spawn(item.transform, false);
                instance.SetIntensity(1);
                instance.Play();
                isRevved = true;
            }
            else if(isRevved && (joint.angle <= 15 && joint.angle >= -15))
            {
                isRevved = false;
            }
        }
        public void FixedUpdate()
        {
            if (!dashing) fallDamage = Player.fallDamage;
            if (Time.time - cdH <= cooldown || !beam || item.rb.velocity.magnitude - Player.local.locomotion.rb.velocity.magnitude < swordSpeed)
            {
                return;
            }
            else
            {
                cdH = Time.time;
                Catalog.GetData<ItemData>("RedQueenBeam").SpawnAsync(null, item.flyDirRef.position, Quaternion.LookRotation(item.flyDirRef.forward, item.rb.velocity.normalized - Player.local.locomotion.rb.velocity.normalized));
            }
        }
    }
}
