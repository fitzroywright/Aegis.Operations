internal static class GlossyCss
{
    internal const string Value = """
html,body{background:radial-gradient(circle at 50% -15%,#153f61 0,#061d30 27%,#00101c 60%,#00070d 100%)}
body:before{content:'';position:fixed;inset:0;pointer-events:none;z-index:9999;background:linear-gradient(115deg,rgba(255,255,255,.03),transparent 22%,transparent 72%,rgba(77,185,255,.025)),repeating-linear-gradient(0deg,rgba(255,255,255,.012) 0,rgba(255,255,255,.012) 1px,transparent 1px,transparent 4px);mix-blend-mode:screen}
.elbow,.brand{background:linear-gradient(180deg,#ffd17a 0,#ff9f42 38%,#d85f15 100%);box-shadow:inset 0 2px 2px rgba(255,255,255,.65),inset 0 -8px 15px rgba(92,29,0,.36),0 5px 18px rgba(0,0,0,.48),0 0 16px rgba(255,153,102,.22)}
.brand{text-shadow:0 1px 0 rgba(255,255,255,.42),0 2px 3px rgba(0,0,0,.35)}
.rail{background:linear-gradient(180deg,#ffbd73 0 18%,#02070b 18% 20%,#b6a7ff 20% 36%,#02070b 36% 38%,#80d6ff 38% 54%,#02070b 54% 56%,#ffb329 56% 72%,#02070b 72% 74%,#e5a9e5 74%);box-shadow:inset 3px 0 2px rgba(255,255,255,.3),inset -10px 0 20px rgba(0,0,0,.3),0 8px 22px rgba(0,0,0,.45)}
.rail .tag{background:linear-gradient(180deg,#0b2d47,#020d16);border:1px solid #2f7097;box-shadow:inset 0 1px 0 rgba(157,220,255,.2),0 3px 7px rgba(0,0,0,.4)}
.main{background:linear-gradient(145deg,rgba(5,32,51,.55),rgba(0,10,18,.72));border-radius:0 18px 18px 0;box-shadow:inset 0 1px 0 rgba(130,205,245,.12)}
.seg{background:linear-gradient(180deg,#c8b9ff,#8e7fe9 52%,#5e52b6);box-shadow:inset 0 2px 1px rgba(255,255,255,.52),inset 0 -4px 8px rgba(37,26,101,.4),0 3px 7px rgba(0,0,0,.4)}
.seg:nth-child(2){background:linear-gradient(180deg,#d4f2ff,#78caff 52%,#3983bd)}.seg:nth-child(3){background:linear-gradient(180deg,#ffd77d,#ff9f16 52%,#b65c00)}
.panel{background:linear-gradient(145deg,rgba(11,53,81,.96),rgba(1,24,40,.98) 52%,rgba(0,11,20,.99));border-color:#bba9df;box-shadow:inset 0 1px 0 rgba(173,227,255,.28),inset 0 -14px 28px rgba(0,0,0,.28),0 7px 20px rgba(0,0,0,.5),0 0 14px rgba(121,193,255,.12)}
.panel:before{background:linear-gradient(90deg,#ffcf72,#ff9f1f 58%,#cb6400);box-shadow:inset 0 2px 1px rgba(255,255,255,.6),0 3px 7px rgba(0,0,0,.35)}
.panel h2{text-shadow:0 2px 2px #000,0 0 15px rgba(111,201,255,.2)}
.status{padding:10px 14px;border-radius:7px;background:linear-gradient(180deg,#0c2d45,#031521);box-shadow:inset 0 1px 0 rgba(151,218,255,.2),0 3px 8px rgba(0,0,0,.35)}
.available{text-shadow:0 0 9px rgba(153,255,178,.45)}.unavailable{text-shadow:0 0 9px rgba(255,102,102,.4)}
.button{background:linear-gradient(180deg,#ffe4ad,#ffca7d 48%,#dc9348);border:1px solid #fff0c8;box-shadow:inset 0 2px 1px rgba(255,255,255,.65),inset 0 -5px 10px rgba(109,58,8,.28),0 4px 10px rgba(0,0,0,.42);text-shadow:0 1px 0 rgba(255,255,255,.5)}
.diag .button{background:linear-gradient(180deg,#d6c7ff,#9e8cff 48%,#6250bf);border-color:#e9e0ff}.button:hover{filter:brightness(1.15)}
.footerElbow,.footer span{background:linear-gradient(180deg,#d4f2ff,#75c9ff 48%,#3983bd);box-shadow:inset 0 2px 1px rgba(255,255,255,.5),inset 0 -4px 8px rgba(12,66,104,.3)}.footer span:nth-child(2){background:linear-gradient(180deg,#ffd77d,#ff9f16,#b65c00)}.footer span:nth-child(3){background:linear-gradient(180deg,#e9c7e9,#c58bc5,#805580)}
@media(prefers-reduced-motion:no-preference){.available{animation:opsPulse 2.8s ease-in-out infinite}@keyframes opsPulse{50%{filter:brightness(1.28)}}}
""";
}
