using System.Collections;
using System.Collections.Generic;
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
        public Item item;
        public Item sword;
        public Creature user;
        public Color beamColor;
        public Color beamEmission;
        public Vector3 beamSize;
        public float despawnTime;
        public float beamSpeed;
        public float beamDamage;
        public bool dismember;
        public Vector3 beamScaleUpdate;
        List<RagdollPart> parts = new List<RagdollPart>();
        public Imbue imbue;
        public void Start()
        {
            item = GetComponent<Item>();
            item.Despawn(despawnTime);
            item.disallowDespawn = true;
            item.renderers[0].material.SetColor("_BaseColor", beamColor);
            item.renderers[0].material.SetColor("_EmissionColor", beamEmission * 2f);
            item.renderers[0].gameObject.transform.localScale = beamSize;
            item.mainCollisionHandler.ClearPhysicModifiers();
            item.physicBody.useGravity = false;
            item.physicBody.drag = 0;
            item.IgnoreRagdollCollision(Player.currentCreature.ragdoll);
            item.RefreshCollision(true);
            item.Throw();
            imbue = item.colliderGroups[0].imbue;
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
        public void Update()
        {
            item.gameObject.transform.localScale += beamScaleUpdate * (Time.deltaTime * 100);
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
            if (c.GetComponentInParent<Breakable>() is Breakable breakable)
            {
                if (item.physicBody.velocity.sqrMagnitude < breakable.neededImpactForceToDamage)
                    return;
                float sqrMagnitude = item.physicBody.velocity.sqrMagnitude;
                --breakable.hitsUntilBreak;
                if (breakable.canInstantaneouslyBreak && sqrMagnitude >= breakable.instantaneousBreakVelocityThreshold)
                    breakable.hitsUntilBreak = 0;
                breakable.onTakeDamage?.Invoke(sqrMagnitude);
                if (breakable.IsBroken || breakable.hitsUntilBreak > 0)
                    return;
                breakable.Break();
            }
            if (c.GetComponentInParent<ColliderGroup>() is ColliderGroup group && group.collisionHandler.isRagdollPart)
            {
                RagdollPart part = group.collisionHandler.ragdollPart;
                if (part.ragdoll.creature != user && part.ragdoll.creature.gameObject.activeSelf == true && !part.isSliced)
                {
                    CollisionInstance instance = new CollisionInstance(new DamageStruct(DamageType.Slash, beamDamage))
                    {
                        targetCollider = c,
                        targetColliderGroup = group,
                        sourceColliderGroup = item.colliderGroups[0],
                        sourceCollider = item.colliderGroups[0].colliders[0],
                        casterHand = sword?.lastHandler?.caster,
                        impactVelocity = item.physicBody.velocity,
                        contactPoint = c.transform.position,
                        contactNormal = -item.physicBody.velocity
                    };
                    instance.damageStruct.penetration = DamageStruct.Penetration.None;
                    instance.damageStruct.hitRagdollPart = part;
                    if (part.sliceAllowed && !part.ragdoll.creature.isPlayer && dismember)
                    {
                        Vector3 direction = part.GetSliceDirection();
                        float num1 = Vector3.Dot(direction, item.transform.up);
                        float num2 = 1f / 3f;
                        if (num1 < num2 && num1 > -num2 && !parts.Contains(part))
                        {
                            parts.Add(part);
                        }
                    }
                    if (imbue?.spellCastBase?.GetType() == typeof(SpellCastLightning))
                    {
                        part.ragdoll.creature.TryElectrocute(1, 2, true, true, (imbue.spellCastBase as SpellCastLightning).imbueHitRagdollEffectData);
                        imbue.spellCastBase.OnImbueCollisionStart(instance);
                    }
                    if (imbue?.spellCastBase?.GetType() == typeof(SpellCastProjectile))
                    {
                        instance.damageStruct.damage *= 2;
                        imbue.spellCastBase.OnImbueCollisionStart(instance);
                    }
                    if (imbue?.spellCastBase?.GetType() == typeof(SpellCastGravity))
                    {
                        imbue.spellCastBase.OnImbueCollisionStart(instance);
                        part.ragdoll.creature.TryPush(Creature.PushType.Hit, item.physicBody.velocity, 3, part.type);
                        part.physicBody.AddForce(item.physicBody.velocity, ForceMode.VelocityChange);
                    }
                    else
                    {
                        if (imbue?.spellCastBase != null && imbue.energy > 0)
                        {
                            imbue.spellCastBase.OnImbueCollisionStart(instance);
                        }
                        part.ragdoll.creature.TryPush(Creature.PushType.Hit, item.physicBody.velocity, 1, part.type);
                    }
                    part.ragdoll.creature.Damage(instance);
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
                item.physicBody.rigidBody.detectCollisions = false;
                item.mainHandler.physicBody.rigidBody.detectCollisions = false;
                item.mainHandler.otherHand.physicBody.rigidBody.detectCollisions = false;
            }
            yield return new WaitForSeconds(DashTime);
            if (DisableGravity)
                Player.local.locomotion.rb.useGravity = true;
            if (DisableCollision)
            {
                Player.local.locomotion.rb.detectCollisions = true;
                item.physicBody.rigidBody.detectCollisions = true;
                item.mainHandler.physicBody.rigidBody.detectCollisions = true;
                item.mainHandler.otherHand.physicBody.rigidBody.detectCollisions = true;
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
                EffectInstance instance = Catalog.GetData<EffectData>("RedQueenRev").Spawn(item.transform, null, false);
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
            if (Time.time - cdH <= cooldown || !beam || item.physicBody.velocity.magnitude - Player.local.locomotion.rb.velocity.magnitude < swordSpeed)
            {
                return;
            }
            else
            {
                cdH = Time.time; 
                Catalog.GetData<ItemData>("RedQueenBeam").SpawnAsync(beam =>
                {
                    BeamCustomization beamCustomization = beam.GetComponent<BeamCustomization>();
                    beamCustomization.sword = item;
                    beamCustomization.user = item.mainHandler != null ? item.mainHandler?.creature : item.lastHandler?.creature;
                    if (beamCustomization.user?.player != null) beam.physicBody.AddForce(Player.local.head.transform.forward * beamCustomization.beamSpeed, ForceMode.Impulse);
                    else if (beamCustomization.user?.brain?.currentTarget is Creature target) beam.physicBody.AddForce(-(beam.transform.position - target.ragdoll.targetPart.transform.position).normalized * beamCustomization.beamSpeed, ForceMode.Impulse);
                    else beam.physicBody.AddForce(beamCustomization.user.ragdoll.headPart.transform.forward * beamCustomization.beamSpeed, ForceMode.Impulse);
                    beam.physicBody.angularVelocity = Vector3.zero;
                    if (item.colliderGroups[0]?.imbue is Imbue imbue && imbue.spellCastBase != null && imbue.energy > 0)
                        beam.colliderGroups[0]?.imbue.Transfer(imbue.spellCastBase, beam.colliderGroups[0].imbue.maxEnergy);
                }, item.flyDirRef.position, Quaternion.LookRotation(item.flyDirRef.forward, item.physicBody.GetPointVelocity(item.flyDirRef.position).normalized));
            }
        }
    }
}
