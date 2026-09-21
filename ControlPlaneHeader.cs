internal static class ControlPlaneHeader
{
    internal const string Css = """
/* Canonical Operations Control Plane header.
   All Operations surfaces inject this block so header geometry/branding changes are made once. */
.lcars-top{position:fixed;left:8px;right:18px;top:8px;height:140px;z-index:20;background:#000}
.lcars-top .left-vertical,.lcars-top>.left-vertical{position:absolute;left:0;top:0;width:160px;height:140px;background:#5588ee;border-radius:28px 0 0 28px}
.lcars-top .left-gold,.lcars-top .join-dot{display:none}
.lcars-top .bottom-arm,.lcars-top>.bottom-arm{position:absolute;left:160px;right:0;top:126px;height:14px;background:#5588ee;z-index:1;border-radius:0 9px 9px 0;box-shadow:none}
.lcars-top::after{content:"";position:absolute;left:160px;top:108px;width:18px;height:18px;background:radial-gradient(circle at 100% 0,transparent 0 17px,#5588ee 18px);z-index:4;pointer-events:none}
.lcars-top .brand{position:absolute;left:178px;top:8px;width:320px;height:96px;z-index:3}
.lcars-top .brand .section{font-size:36px;line-height:1;color:#66ccff;font-weight:900;text-transform:uppercase;margin-top:10px;white-space:nowrap}
.lcars-top .brand .org{font-size:10px;color:#99ccff;font-weight:800;text-transform:uppercase;margin-top:5px;max-width:220px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.lcars-top .brand .datetime{margin-top:8px;font-weight:900;text-transform:uppercase;color:#66ccff;letter-spacing:.04em;font-size:14px;white-space:nowrap}
.lcars-top .brand .brand-user{margin-top:4px;color:#99ccff;font:800 10px/1 Arial,sans-serif;letter-spacing:.04em;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:260px}
.lcars-top .live-log{position:absolute;left:470px;top:8px;right:318px;height:108px;overflow:hidden;font:13px/1.35 ui-monospace,SFMono-Regular,Consolas,monospace;padding:0}
.lcars-top .live-log .recent-title{display:flex;align-items:center;width:100%;height:18px;background:#ffcc66;color:#071018;font:900 10px/18px Arial,sans-serif;letter-spacing:.12em;text-transform:uppercase;padding:0 8px;margin:0;border-radius:3px 3px 0 0}
.lcars-top .top-buttons{position:absolute;right:50px;top:18px;width:250px;height:72px;display:grid;grid-template-columns:1fr 1fr;gap:8px 10px;padding-left:12px}
.lcars-top .top-buttons a,.lcars-top .top-buttons button{border:0;border-radius:8px;font-weight:900;text-transform:uppercase;color:#071018;cursor:pointer;font-size:12px;display:flex;align-items:center;justify-content:center;line-height:1;padding:0 8px;box-shadow:inset 0 1px 0 rgba(255,255,255,.28),0 1px 0 rgba(0,0,0,.55)}
.lcars-top .top-buttons .gold{background:#88eeff}.lcars-top .top-buttons .blue{background:#5588ee}.lcars-top .top-buttons .salmon{background:#7799dd}.lcars-top .top-buttons .gray{background:#99ccff}.lcars-top .top-buttons .logout{background:#ff3500;color:#fff}
/* Control Plane nucleus mark: concentric orbits with deliberately asymmetric electrons. */
.lcars-top .left-vertical::after{content:"";position:absolute;left:26px;top:16px;width:108px;height:108px;background:url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 108 108'%3E%3Cg fill='none' stroke='%23bfeaff' stroke-width='1.2' opacity='.7'%3E%3Ccircle cx='54' cy='54' r='25'/%3E%3Ccircle cx='54' cy='54' r='38'/%3E%3Ccircle cx='54' cy='54' r='49'/%3E%3C/g%3E%3Cg fill='%23071018' stroke='%2366ccff' stroke-width='1.4'%3E%3Crect x='40' y='43' width='28' height='7' rx='1'/%3E%3Crect x='40' y='51' width='28' height='7' rx='1'/%3E%3Crect x='40' y='59' width='28' height='7' rx='1'/%3E%3C/g%3E%3Cg fill='%2366ccff'%3E%3Ccircle cx='64' cy='46.5' r='1.3'/%3E%3Ccircle cx='64' cy='54.5' r='1.3'/%3E%3Ccircle cx='64' cy='62.5' r='1.3'/%3E%3C/g%3E%3Cg stroke-width='2'%3E%3Ccircle cx='30' cy='32' r='8' fill='%23071018' stroke='%23ffb43d'/%3E%3Ccircle cx='82' cy='24' r='8' fill='%23071018' stroke='%2332e79a'/%3E%3Ccircle cx='88' cy='76' r='8' fill='%23071018' stroke='%23b99aff'/%3E%3Ccircle cx='26' cy='83' r='8' fill='%23071018' stroke='%2366ccff'/%3E%3C/g%3E%3Cg fill='none' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.7'%3E%3Cpath d='M26 34h8m-6-4h4v8h-4z' stroke='%23ffb43d'/%3E%3Cpath d='M79 20h6v8h-6zm2 2h2m-2 3h2' stroke='%2332e79a'/%3E%3Cpath d='M83 76h3l2-4 3 8 2-4h3' stroke='%23b99aff'/%3E%3Cpath d='M22 83h8m-4-4v8' stroke='%2366ccff'/%3E%3C/g%3E%3C/svg%3E") center/contain no-repeat;pointer-events:none;filter:drop-shadow(0 2px 2px rgba(0,0,0,.45))}
@media(max-width:1250px){.lcars-top .brand{width:280px}.lcars-top .live-log{left:410px;right:289px}.lcars-top .top-buttons{width:225px}}
""";
}
