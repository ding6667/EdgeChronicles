# -*- coding: utf-8 -*-
"""判据 oracle 的 Python 镜像（不需要 Unity，1 秒跑完）。

【它是什么】
`Assets/_Project/Tests/Shared/Qa/CriterionOracle.cs` 的**逐条对应**的 Python 复刻。
用来做两件事：
  ① 证伪：43 条负向注入在镜像上跑一遍，确认判据"抓得住错、也不误杀对的"；
  ② 回归护栏：改判据 / 改夹具后，镜像先响，不用等 Unity 编译。

【纪律】
  · 镜像必须与 C# oracle **逐条对应**。改了一边就要改另一边——
    否则 lint 会报绿，而绿的是一个**过期的模型**（假通过）。
  · 镜像里的阈值一律来自 CombatSpec 对应的常量，**不许写裸数字**。
  · 两种曲线口径（逐帧速度 m/s / 归一化位移占比 Σ=1）都要跑：
    两条裁定尚未收敛（见 KnownSpecConflicts D-02 / D-13），任一口径下 43 条都要全绿。

【口径来源】主理人 2026 裁定（D-02 / D-03 / D-04 / D-05 / D-10 / D-11 / D-12）
"""

import copy
import sys

import copy
TAGS=["core","light","heavy","dash","control","guard","channel","finisher"]
SKILL_IDS=["sk_shifeng","sk_xunfeng","sk_lieshi","sk_huifeng","sk_fufeng","sk_baoyuan","sk_jufeng","sk_pofeng"]
PH=[(16,6,22),(8,3,10),(20,8,24),(10,6,16),(12,5,18),(8,12,14),(10,45,12),(14,8,20)]
SHARED_ACTION_TAGS=["dodge","attack"]
ALL_UNLOCK_TYPES=["stat","resource","derivative","window","mechanic"]
TIER_ORDERS=[
 ["stat","resource","derivative","window","mechanic"],
 ["stat","window","derivative","resource","mechanic"],
 ["stat","window","resource","derivative","mechanic"],
 ["stat","window","derivative","resource","mechanic"],
 ["stat","derivative","resource","window","mechanic"],
 ["stat","resource","derivative","window","mechanic"],
 ["stat","resource","derivative","window","mechanic"],
 ["stat","resource","derivative","window","mechanic"]]
CANCEL_RULES={("core","light"):"F",("core","heavy"):"F",("core","dash"):"F",("core","control"):"F",("core","guard"):"F",
("core","channel"):"F",("core","finisher"):"F",("finisher","heavy"):"F",("finisher","dash"):"F",("finisher","control"):"F",
("finisher","guard"):"F",("finisher","channel"):"F",("finisher","finisher"):"F",("heavy","guard"):"F",("guard","core"):"F",
("channel","heavy"):"F",("heavy","core"):"H",("heavy","dash"):"H",("heavy","control"):"H",("heavy","channel"):"H",
("heavy","finisher"):"H",("control","core"):"H",("control","channel"):"H",("guard","heavy"):"H",("guard","finisher"):"H"}
TRIGGERS={"OnFightStart","OnTimer","OnHpBelow","OnPhaseEnter","OnCastComplete","OnAddSpawned","OnPlayerSkillHit","OnPlayerEnterZone","OnAnchorStateChanged"}
CONDS={"HpBelow","HpAbove","PhaseEquals","PlayerInZone","AnchorInState","CooldownReady","StackCountGte","RandomPct","AggroCountGte"}
EFFECTS={"SpawnHazard","MovePlayer","ModifyTerrain","ForcePhase","ApplyBuff","SpawnAdd","PlayVfx","ShakeCamera","LockZone"}
MECH=set(["MM_ATK_NO_INTERRUPT_IN_DOMAIN","MM_FINISH_COST_MINUS1","MM_PERFECT_DODGE_REFUND1","MM_RESOURCE_CAP_PLUS1",
"MM_CORE_CD_MINUS_FRAMES","MM_DOMAIN_LAST_JUMP_DOUBLE","MM_CHANNEL_INTERVAL_HALF","MM_DASH_IFRAME_PLUS_FRAMES",
"MM_BIND_TRIGGER_VARIANT","MM_FINISH_EXTRA_DOMAIN","MM_DOMAIN_HIT_GAIN1","MM_GUARD_REFUND_IN_DOMAIN","MM_HEAVY_GAIN_EXTRA",
"MM_CANCEL_WINDOW_WIDEN","MM_DODGE_CD_MINUS_FRAMES","MM_CRIT_IN_DOMAIN","MM_STAR_BREAKSTATE","MM_STAR_SHIELD_BREAK_FIRST",
"MM_STAR_LIFESTEAL_CONTINUE","MM_STAR_CHANNEL_MOVE","MM_STAR_OVERCAP_TO_DAMAGE","MM_STAR_CANCEL_ALL_AFTER_N",
"MM_MER_CONDUCE_SKILL","MM_MER_CONDUCE_RANGE","MM_MER_CONDUCE_STACK","MM_MER_STACK_REFUND","MM_MER_CONDUCE_FINISH","MM_MER_FINISH_CHAIN"])
C=dict(WorstCaseFps=30.0,LogicFps=60,HitstopTimeScale=0.15,HoldDurationMin=2.0,HoldDurationMax=4.0,KbPeakMax=18.0,
KbPerFrameMax=0.5,RecoveryGapMin=2.0,DodgeInvulnMaxSpan=14,BladeOcclusionMax=0.30,PulseHzMax=3.0,InputLatencyMaxMs=50,
CpMinRatio=1.3,CurveTol=0.05,HitstopMin=0.05,HitstopMax=0.25,VfxBudget=8,VfxGeneric=2,DerivativeNewGraphicCount=6,
RecoveryStartBase=9.0,RearWindow=6,DodgePushMax=0.05,HitFeedbackMax=3,KbDirTol=45.0,InkContrastMin=0.6,InkPx=3,
HitClipHeightMin=3.0,HitClipFramesMin=2,HitClipVariantsMin=4,Dt=1.0/60.0)
CURVE_SEMANTICS="speed"   # "speed"(m/s) | "share"(Σ=1)

def make_curve(cid, declared, n, scale=1.0):
    shape=[1.0-(i/(n-1.0))*0.6 for i in range(n)]; tot=sum(shape); c=dict(id=cid,declared=declared,keys=[])
    for sh in shape:
        share=sh/tot
        c["keys"].append((share*declared/C["Dt"]) if CURVE_SEMANTICS=="speed" else share*scale)
        if CURVE_SEMANTICS=="speed": c["keys"][-1]*=scale
    return c

def baseline():
    s=dict(skills=[],cancels=[],curves=[],bosses=[],rules=[],anchors=[],dungeons=[],vfxspecs=[],vfxassets=[],
            kbs=[],derivs=[],drops=[],stars=[],effects=[],meridians=[],
            dodge=dict(start=2,end=16,move=16,total=22))
    for i,(st,ac,rc) in enumerate(PH):
        s["skills"].append(dict(id=SKILL_IDS[i],startup=st,active=ac,recovery=rc,tags=[TAGS[i]],
            shapes=[dict(frame=0,hitstop=0.15)],writes=(i==0),consumes=(i==7),deriv_ids=[]))
        s["cancels"].append(dict(src=TAGS[i],allow="dodge",frm=st+ac,to=st+ac+rc-1))
    s["curves"]=[make_curve("mc_xunfeng",3.0,5),make_curve("mc_huifeng",3.0,4)]
    s["dungeons"]=[dict(id="luoxia",cp=100,pw=390),dict(id="tiekuang",cp=900,pw=930),dict(id="gufeng",cp=2400,pw=2604)]
    s["anchors"]=["anchor://puzzle/pillar_01","anchor://puzzle/pillar_02","anchor://arena/web","anchor://arena/main"]
    s["bosses"]=[dict(id="boss_01_shifo",phases=3,arena="anchor://arena/main"),
                 dict(id="boss_02_gumu",phases=3,arena="anchor://arena/web"),
                 dict(id="boss_03_tieyao",phases=4,arena="anchor://arena/main")]
    s["rules"]=[dict(id="br_shifo_01",boss="boss_01_shifo",phase=2,trig="OnAnchorStateChanged",conds=["PhaseEquals:2"],
                 eff=["ModifyTerrain:zone_pit_a:Open","SpawnHazard:hz_rock:3"],anchors=["anchor://puzzle/pillar_02"],fpt=0),
                dict(id="br_gumu_01",boss="boss_02_gumu",phase=1,trig="OnTimer",conds=["StackCountGte:brood:3"],
                 eff=["SpawnAdd:em_broodling:2","MovePlayer:PullTo:anchor://arena/web:3.0"],anchors=["anchor://arena/web"],fpt=0),
                dict(id="br_gumu_02",boss="boss_02_gumu",phase=1,trig="OnAddSpawned",conds=[],
                 eff=["ApplyBuff:Self:bf_frenzy:1"],anchors=[],fpt=0),
                dict(id="br_tieyao_01",boss="boss_03_tieyao",phase=3,trig="OnPlayerSkillHit",conds=["HpBelow:0.3"],
                 eff=["LockZone:zone_arena:Close","ForcePhase:4","ShakeCamera:0.6:18"],anchors=[],fpt=4)]
    for vid,px,st,wf in [("vfx_armor_generic",4,0,12),("vfx_invuln_generic",2,1,12),("vfx_armor_pofeng",4,0,12),
                         ("vfx_invuln_pofeng",2,1,12),("vfx_armor_xunfeng",4,0,12),("vfx_invuln_xunfeng",2,1,12)]:
        s["vfxassets"].append(dict(id=vid,px=px,style=st,hz=0.0,wf=wf))
    for sid in SKILL_IDS:
        a="vfx_armor_pofeng" if sid=="sk_pofeng" else ("vfx_armor_xunfeng" if sid=="sk_xunfeng" else "vfx_armor_generic")
        iv="vfx_invuln_pofeng" if sid=="sk_pofeng" else ("vfx_invuln_xunfeng" if sid=="sk_xunfeng" else "vfx_invuln_generic")
        s["vfxspecs"].append(dict(skill=sid,armor=a,invuln=iv))
    for tier,(hs,hold,mn,mx,peak,hf) in {"light":(18,3.3,0.3,0.6,5.3,2),"medium":(20,3.6,0.8,1.2,10.6,4),"heavy":(22,3.9,1.2,1.5,13.2,6)}.items():
        clip_c, fps = 13, 60
        clip_w = clip_c*60.0/fps
        hold_calc = hs - clip_w - hf*(1-0.15)
        s["kbs"].append(dict(tier=tier,hitstun=hs,hitstop=hf,scale=0.15,hold=hold_calc,declared_hold=hold_calc,
                             clip_c=clip_c,fps=fps,rear=C["RearWindow"],dmin=mn,dmax=mx,
                             split=hf,curve=hf+C["RearWindow"],rec=C["RecoveryStartBase"],peak=peak))
    ng={"sk_shifeng":3,"sk_xunfeng":3,"sk_fufeng":2,"sk_baoyuan":5,"sk_jufeng":5,"sk_pofeng":5}
    ngid={"sk_shifeng":"dv_a","sk_xunfeng":"dv_b","sk_fufeng":"dv_c","sk_baoyuan":"dv_d","sk_jufeng":"dv_e","sk_pofeng":"dv_f"}
    s["deffx"]=[]
    for sid,tier in ng.items():
        s["deffx"].append(dict(skill=sid,tier=tier,did=ngid[sid],vt="new_graphic"))
    s["deffx"].append(dict(skill="sk_shifeng",tier=3,did="dv_shifeng_generic",vt="generic"))
    s["deffx"].append(dict(skill="sk_xunfeng",tier=3,did="dv_xunfeng_generic",vt="generic"))
    for i,sid in enumerate(SKILL_IDS):
        order=TIER_ORDERS[i]
        for tier in range(1,6):
            ut=order[tier-1]
            vt = "generic" if ut=="derivative" else ("param" if ut=="window" else "none")
            for e in s["deffx"]:
                if e["skill"]==sid and e["tier"]==tier and e["vt"]=="new_graphic": vt="new_graphic"
            s["derivs"].append(dict(skill=sid,tier=tier,unlock=ut,vt=vt,targets=["loop_l4"] if tier==5 else []))
    for i,sid in enumerate(SKILL_IDS):
        for e in s["deffx"]:
            if e["skill"]==sid: s["skills"][i]["deriv_ids"].append(e["did"])
    s["stars"]=[dict(id="sn_blade_1",pre=[]),dict(id="sn_blade_2",pre=["sn_blade_1"])]
    s["meridians"]=[dict(id="mn_hub_1",mm="MM_MER_CONDUCE_STACK"),dict(id="mn_branch_1",mm="MM_MER_CONDUCE_RANGE")]
    s["effects"]=[dict(key="resource_cap",cls="cap",src=[1.0,1.0],merged=1.0),
                  dict(key="atk_flat",cls="num",src=[5.0,8.0],merged=5.0),
                  dict(key="crit_flag",cls="disc",src=[1.0],merged=1.0)]
    s.update(resource=0,independent=True,inj_lat=0,meas_lat=0.0,occl=0.18,coverage=0.005,fps=30.0,push=0.03,
        feedback=2,kbdir=180.0,dmgdir=0.0,wall=False,fall=False,tele=30,t_circle=True,t_shape=True,t_audio=True,
        ink=0.72,inkpx=3,slots=1,addmat=False,clearcoat=False,elite_col=1.4,elite_vis=1.4,
        clipvars=4,clip_h=4.0,clip_f=2,d5_color=True,d5_form=True,d5_time=True)
    return s

def computed_hold(k):
    clip_w = k["clip_c"]*60.0/k["fps"]
    return k["hitstun"] - clip_w - k["hitstop"]*(1-k["scale"])

def evaluate(s):
    fired=set(); add=fired.add
    for sk in s["skills"]:
        tot=sk["startup"]+sk["active"]+sk["recovery"]
        if sk["startup"]<1 or sk["active"]<1 or sk["recovery"]<1 or tot>90: add("A19")
        if not sk["shapes"]: add("A23")
        for sh in sk["shapes"]:
            if sh["frame"]>=sk["active"]: add("A20")
            if sh["hitstop"]<C["HitstopMin"] or sh["hitstop"]>C["HitstopMax"]: add("A30")
        if sk["writes"] and sk["consumes"]: add("A25")
    for w in s["cancels"]:
        if w["frm"]>w["to"]: add("A21")
        tot=0
        for sk in s["skills"]:
            if sk["tags"][0]==w["src"]: tot=sk["startup"]+sk["active"]+sk["recovery"]
        if tot>0 and w["to"]>=tot: add("A21")
    known=set(t for sk in s["skills"] for t in sk["tags"]) | set(SHARED_ACTION_TAGS)
    for w in s["cancels"]:
        if w["allow"] not in known: add("A22")
    for c in s["curves"]:
        integral = sum(c["keys"])*C["Dt"] if CURVE_SEMANTICS=="speed" else sum(c["keys"])*c["declared"]
        if abs(integral-c["declared"])/c["declared"]>C["CurveTol"]: add("A24")
    for r in s["rules"]:
        if r["trig"] not in TRIGGERS: add("A26")
        for cnd in r["conds"]:
            if cnd.split(":")[0] not in CONDS: add("A26")
        for e in r["eff"]:
            if e.split(":")[0] not in EFFECTS: add("A26")
        if r["fpt"]>0:
            ph=[b["phases"] for b in s["bosses"] if b["id"]==r["boss"]]
            if ph and r["fpt"]>ph[0]: add("A27")
        for a in r["anchors"]:
            if a not in s["anchors"]: add("A28")
        for e in r["eff"]:
            if e.startswith("MovePlayer:"):
                p=e.split(":")
                if len(p)>=3 and p[2].startswith("anchor://") and p[2] not in s["anchors"]: add("A28")
    for b in s["bosses"]:
        if b["arena"] not in s["anchors"]: add("A28")
    if s["resource"]<0: add("A29")
    indeg={n["id"]:0 for n in s["stars"]}; ch={n["id"]:[] for n in s["stars"]}
    for n in s["stars"]:
        for p in n["pre"]:
            if p in ch: ch[p].append(n["id"]); indeg[n["id"]]+=1
    q=[i for i in indeg if indeg[i]==0]; vis=0
    while q:
        cur=q.pop(0); vis+=1
        for c2 in ch[cur]:
            indeg[c2]-=1
            if indeg[c2]==0: q.append(c2)
    if vis!=len(s["stars"]): add("A31")
    # A33 阶位不变量
    bysk={}
    for d in s["derivs"]: bysk.setdefault(d["skill"],[]).append(d)
    for sid,rows in bysk.items():
        if len(rows)!=5: add("A33"); continue
        types=set(d["unlock"] for d in rows)
        if len(types)!=5: add("A33")
        for req in ALL_UNLOCK_TYPES:
            if req not in types: add("A33")
        t1=[d for d in rows if d["tier"]==1]; t5=[d for d in rows if d["tier"]==5]
        if not t1 or t1[0]["unlock"]!="stat": add("A33")
        if not t5 or t5[0]["unlock"]!="mechanic": add("A33")
    declared={}
    for e in s["deffx"]:
        declared.setdefault(e["skill"],[]).append(e["did"])
        sk=[x for x in s["skills"] if x["id"]==e["skill"]][0]
        if e["did"] not in sk["deriv_ids"]: add("A33-1")
    for sk in s["skills"]:
        lst=declared.get(sk["id"],[])
        for did in sk["deriv_ids"]:
            if did not in lst: add("A33-1")
    per={}
    for e in s["deffx"]:
        per[e["skill"]]=per.get(e["skill"],0)+1
        if e["tier"] not in (1,2,3,5): add("A33-2")
        if e["skill"] in ("sk_lieshi","sk_huifeng"): add("A33-3")
    for k,v in per.items():
        if v>2: add("A33-2")
    for d in s["derivs"]:
        if d["unlock"]=="derivative" and d["vt"]=="new_graphic":
            if not any(e["skill"]==d["skill"] and e["tier"]==d["tier"] for e in s["deffx"]): add("A33-4")
    ngc=sum(1 for e in s["deffx"] if e["vt"]=="new_graphic")
    if ngc!=C["DerivativeNewGraphicCount"]: add("A33-5")
    for d in s["derivs"]:
        for t in d["targets"]:
            if not t.startswith("loop_l"): add("A35")
    for m in s["meridians"]:
        if m["mm"] not in MECH: add("A35")
    for e in s["effects"]:
        src=e["src"]
        if e["cls"]=="cap":
            if abs(e["merged"]-max(src))>1e-4: add("A39-1")
        elif e["cls"]=="num":
            mn=min(src); sm=sum(src)
            if len(src)>1 and abs(e["merged"]-sm)<1e-4 and abs(sm-mn)>1e-4: add("A39-2")
            elif abs(e["merged"]-mn)>1e-4: add("A39-2")
        else:
            if not any(abs(e["merged"]-x)<1e-4 for x in src): add("A39-2")
    if ngc+C["VfxGeneric"]>C["VfxBudget"]: add("A43")
    for k in s["kbs"]:
        ch_ = computed_hold(k)
        if ch_ < 0: add("F-HOLD")
        elif ch_ < C["HoldDurationMin"] or ch_ > C["HoldDurationMax"]: add("F-HOLD")
        elif abs(k["declared_hold"]-ch_)>0.05: add("F-HOLD")
        if k["scale"]*k["hitstop"]>1.0: add("F-HITSTOP-CAUSAL")
        tz=k["hitstop"]+k["rear"]; tr=k["rec"]+0.85*k["hitstop"]
        if tz+C["RecoveryGapMin"]>tr: add("F-RECOVERY-GAP")
        lim={"light":(0.3,0.6),"medium":(0.8,1.2),"heavy":(1.2,1.5)}[k["tier"]]
        if k["dmin"]<lim[0]-1e-4 or k["dmax"]>lim[1]+1e-4: add("F-KB-TIER")
        if k["peak"]>C["KbPeakMax"] or k["peak"]/C["WorstCaseFps"]>C["KbPerFrameMax"]+1e-4 or k["peak"]/s["fps"]>C["KbPerFrameMax"]+1e-4:
            add("F-KB-PEAK")
    d=s["dodge"]; span=d["end"]-d["start"]
    if span<1 or span>C["DodgeInvulnMaxSpan"] or d["start"]<1 or d["end"]>d["move"]: add("F-DODGE-INVULN")
    assets={a["id"]:a for a in s["vfxassets"]}
    for sp in s["vfxspecs"]:
        if sp["armor"]==sp["invuln"]: add("F-VFX-STRUCTURE"); continue
        if sp["armor"] not in assets or sp["invuln"] not in assets: add("F-VFX-STRUCTURE"); continue
        if assets[sp["armor"]]["style"]==assets[sp["invuln"]]["style"]: add("F-VFX-STRUCTURE")
    for a in s["vfxassets"]:
        if a["hz"]<0 or a["hz"]>C["PulseHzMax"]: add("F-PULSE-HZ")
        if a["hz"]>0 and a["wf"]*(1000.0/C["LogicFps"])+1e-6 < 1000.0/(2.0*a["hz"]): add("F-PULSE-WINDOW")
    if s["occl"]>C["BladeOcclusionMax"]+1e-6: add("F-BLADE-OCCLUSION")
    if s["meas_lat"]>C["InputLatencyMaxMs"]+1e-6: add("F-E3-LATENCY")
    if not s["independent"]: add("F-E3-SAME-SOURCE")
    if not (s["d5_color"] and s["d5_form"] and s["d5_time"]): add("F-D5-CONTROL")
    if s["push"]<0 or s["push"]>C["DodgePushMax"]+1e-6: add("F-DODGE-PUSH")
    if s["feedback"]<0 or s["feedback"]>C["HitFeedbackMax"]: add("F-HIT-FEEDBACK")
    exp=(s["dmgdir"]+180.0)%360.0; delta=abs(s["kbdir"]-exp)
    if delta>180.0: delta=360.0-delta
    if delta>C["KbDirTol"]+1e-3: add("F-KB-DIRECTION")
    if s["wall"] or s["fall"]: add("F-KB-WALL")
    if s["tele"]<30: add("F-BOSS-TELEGRAPH")
    if not (s["t_circle"] and s["t_shape"] and s["t_audio"]): add("F-TELEGRAPH-CHANNELS")
    if s["ink"]<C["InkContrastMin"]-1e-6 or s["inkpx"]!=C["InkPx"]: add("F-INK-CONTRAST")
    if s["slots"]!=1 or s["addmat"] or s["clearcoat"]: add("F-VFX-CHANNEL")
    if abs(s["elite_col"]-s["elite_vis"])>1e-3: add("F-ELITE-HITBOX")
    if s["clipvars"]<C["HitClipVariantsMin"] or s["clip_h"]<=C["HitClipHeightMin"] or s["clip_f"]<C["HitClipFramesMin"]:
        add("F-HITCLIP-VARIANTS")
    dl=sorted(s["dungeons"],key=lambda x:x["cp"])
    for i,dg in enumerate(dl):
        if dg["cp"]>dg["pw"]: add("F-EDGE-LEVELS")
        if i>0 and (dg["cp"]<=dl[i-1]["cp"] or dg["cp"]<dl[i-1]["cp"]*C["CpMinRatio"]-1e-4): add("F-EDGE-LEVELS")
    return fired

def inject(cid, s):
    if cid=="NC-A01": s["dodge"]["start"]=0; s["dodge"]["end"]=0
    elif cid=="NC-A02": s["feedback"]=5
    elif cid=="NC-A03": s["dmgdir"]=0.0; s["kbdir"]=0.0
    elif cid=="NC-A04": kb(s,"light")["dmax"]=0.8
    elif cid=="NC-A05": s["wall"]=True
    elif cid=="NC-A06": s["tele"]=18
    elif cid=="NC-A07": s["t_audio"]=False
    elif cid=="NC-A08": s["ink"]=0.45
    elif cid=="NC-A09": s["addmat"]=True; s["slots"]=2
    elif cid=="NC-A10": s["elite_col"]=1.0; s["elite_vis"]=1.4
    elif cid=="NC-A11": s["clipvars"]=3; s["clip_h"]=2.0; s["clip_f"]=1
    elif cid=="NC-A12": s["d5_color"]=True; s["d5_form"]=False; s["d5_time"]=False
    elif cid=="NC-A13": s["inj_lat"]=10; s["meas_lat"]=166.7; s["independent"]=True
    elif cid=="NC-A14": s["inj_lat"]=10; s["meas_lat"]=0.0; s["independent"]=False
    elif cid=="NC-A15":
        k=kb(s,"light"); k["clip_c"]=15; syn_hold(k)
    elif cid=="NC-A16":
        k=kb(s,"heavy"); k["hitstop"]=8; k["split"]=8; k["curve"]=8+k["rear"]; syn_hold(k)
    elif cid=="NC-A17": asset(s,"vfx_invuln_pofeng")["style"]=0
    elif cid=="NC-A18": s["vfxassets"][0]["hz"]=4.0
    elif cid=="NC-A19": s["vfxassets"][0]["hz"]=2.0; s["vfxassets"][0]["wf"]=3
    elif cid=="NC-A20": s["occl"]=0.45
    elif cid=="NC-A21": s["push"]=0.12
    elif cid=="NC-A22": s["skills"][0]["startup"]=0
    elif cid=="NC-A23": s["skills"][0]["shapes"][0]["frame"]=s["skills"][0]["active"]
    elif cid=="NC-A24": s["cancels"][0]["allow"]="airborne"
    elif cid=="NC-B01": s["occl"]=0.12; s["coverage"]=0.005
    elif cid=="NC-B02": s["vfxassets"][0]["hz"]=1.0; s["vfxassets"][0]["wf"]=36; s["vfxassets"][1]["hz"]=0.0; s["vfxassets"][1]["wf"]=5
    elif cid=="NC-B03": s["fps"]=60.0; kb(s,"heavy")["peak"]=13.2
    elif cid=="NC-B04":
        for d in s["dungeons"]:
            if d["id"]=="tiekuang": d["cp"]=700
    elif cid=="NC-B05":
        s["deffx"].append(dict(skill="sk_jufeng",tier=3,did="dv_jufeng_extra",vt="generic"))
        for sk in s["skills"]:
            if sk["id"]=="sk_jufeng": sk["deriv_ids"].append("dv_jufeng_extra")
    elif cid=="NC-B06":
        k=kb(s,"heavy"); k["dmin"]=1.2; k["dmax"]=1.5
        s["skills"][0]["shapes"][0]["hitstop"]=C["HitstopMin"]
    else: raise SystemExit("no inject "+cid)

def inject_s(cid,s):
    if cid=="S-01": s["cancels"][0]["frm"]=s["cancels"][0]["to"]+1
    elif cid=="S-02":
        sk=s["skills"][0]; tot=sk["startup"]+sk["active"]+sk["recovery"]
        for w in s["cancels"]:
            if w["src"]==sk["tags"][0]: w["to"]=tot
    elif cid=="S-03": s["skills"][0]["shapes"]=[]
    elif cid=="S-04": s["curves"][0]=make_curve("mc_xunfeng",3.0,5,0.92)
    elif cid=="S-05": s["skills"][0]["writes"]=True; s["skills"][0]["consumes"]=True
    elif cid=="S-06": s["rules"][0]["trig"]="OnSomeUnknownTrigger"
    elif cid=="S-07":
        s["rules"][3]["fpt"]=9
        s["rules"][3]["eff"]=["LockZone:zone_arena:Close","ForcePhase:9","ShakeCamera:0.6:18"]
    elif cid=="S-08": s["rules"][0]["anchors"]=["anchor://puzzle/pillar_99"]
    elif cid=="S-09": s["resource"]=-1
    elif cid=="S-10": s["skills"][0]["shapes"][0]["hitstop"]=0.30
    elif cid=="S-11": s["meridians"][0]["mm"]="MM_SomeFreeTextMechanic"
    elif cid=="S-12":
        for e in s["effects"]:
            if e["cls"]=="cap": e["merged"]=2.0
    elif cid=="S-13":
        for e in s["effects"]:
            if e["cls"]=="num": e["merged"]=13.0
    else: raise SystemExit("no inject "+cid)

TARGET={"NC-A01":"F-DODGE-INVULN","NC-A02":"F-HIT-FEEDBACK","NC-A03":"F-KB-DIRECTION","NC-A04":"F-KB-TIER",
"NC-A05":"F-KB-WALL","NC-A06":"F-BOSS-TELEGRAPH","NC-A07":"F-TELEGRAPH-CHANNELS","NC-A08":"F-INK-CONTRAST",
"NC-A09":"F-VFX-CHANNEL","NC-A10":"F-ELITE-HITBOX","NC-A11":"F-HITCLIP-VARIANTS","NC-A12":"F-D5-CONTROL",
"NC-A13":"F-E3-LATENCY","NC-A14":"F-E3-SAME-SOURCE","NC-A15":"F-HOLD","NC-A16":"F-HITSTOP-CAUSAL",
"NC-A17":"F-VFX-STRUCTURE","NC-A18":"F-PULSE-HZ","NC-A19":"F-PULSE-WINDOW","NC-A20":"F-BLADE-OCCLUSION",
"NC-A21":"F-DODGE-PUSH","NC-A22":"A19","NC-A23":"A20","NC-A24":"A22",
"NC-B01":"F-BLADE-OCCLUSION","NC-B02":"F-PULSE-WINDOW","NC-B03":"F-KB-PEAK","NC-B04":"F-EDGE-LEVELS",
"NC-B05":"A33-1","NC-B06":"F-HOLD"}
ST={"S-01":"A21","S-02":"A21","S-03":"A23","S-04":"A24","S-05":"A25","S-06":"A26","S-07":"A27",
"S-08":"A28","S-09":"A29","S-10":"A30","S-11":"A35","S-12":"A39-1","S-13":"A39-2"}

COMPANIONS={"NC-A16":{"F-RECOVERY-GAP"},"NC-A22":{"A21"}}


def main():
    failures = []
    for sem in ("speed", "share"):
        global CURVE_SEMANTICS
        CURVE_SEMANTICS = sem
        base = baseline()
        fired = evaluate(base)
        if fired:
            failures.append("[%s] 合法基线不应报红，却报红：%s" % (sem, sorted(fired)))

        for cid in sorted(TARGET):
            s = copy.deepcopy(base)
            inject(cid, s)
            got = evaluate(s)
            tgt = TARGET[cid]
            if cid.startswith("NC-A") or cid.startswith("S-"):
                if tgt not in got:
                    failures.append("[%s] %s：目标 %s 未报红，实际 %s —— 漏挂即废判据"
                                    % (sem, cid, tgt, sorted(got)))
                extra = set(got) - set([tgt]) - set(COMPANIONS.get(cid, set()))
                if extra:
                    failures.append("[%s] %s：额外报红 %s（目标只有 %s）" % (sem, cid, sorted(extra), tgt))
            else:
                if tgt in got:
                    failures.append("[%s] %s（B 类）：目标 %s 报红了 —— 判据误杀了一个合法实现" % (sem, cid, tgt))
                if got:
                    failures.append("[%s] %s（B 类）：注入后整张表不合法，报红 %s" % (sem, cid, sorted(got)))

        for cid in sorted(ST):
            s = copy.deepcopy(base)
            inject_s(cid, s)
            got = evaluate(s)
            tgt = ST[cid]
            if tgt not in got:
                failures.append("[%s] %s：目标 %s 未报红，实际 %s —— 漏挂即废判据" % (sem, cid, tgt, sorted(got)))
            extra = set(got) - set([tgt])
            if extra:
                failures.append("[%s] %s：额外报红 %s（目标只有 %s）" % (sem, cid, sorted(extra), tgt))

    if failures:
        print("[FAIL ] 负向注入自检失败：")
        for f in failures:
            print("       " + f)
        return 1

    print("[OK   ] 合法基线零报红；30 条规范集（24 A 类 + 6 B 类）+ 13 条补充集"
          "在两种曲线口径下全部符合预期")
    return 0


if __name__ == "__main__":
    sys.exit(main())
