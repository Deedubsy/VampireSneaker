var name = "__LIGHT__"; bool before = __BEFORE__;
var gl = UnityEngine.GameObject.Find(name).GetComponent<Vespertine.Stealth.GameLight>();
var u = gl.GetComponentInChildren<UnityEngine.Light>();
var ls = Vespertine.Core.Game.Lights;
float c = ls.ExposureRadius(gl), h = gl.Height;
var keep = new { ck = u.cookie, a = u.spotAngle, i = u.innerSpotAngle, r = u.range, it = u.intensity };
if (before) { float e = c + 0.3f; u.cookie = null; u.innerSpotAngle = 2f*UnityEngine.Mathf.Atan2(c,h)*UnityEngine.Mathf.Rad2Deg; u.spotAngle = 2f*UnityEngine.Mathf.Atan2(e,h)*UnityEngine.Mathf.Rad2Deg; u.range = UnityEngine.Mathf.Sqrt(e*e+h*h)*1.3f; u.intensity = 3.2f*1.3f*gl.Intensity; }
foreach (var n in Vespertine.Core.Game.AI.Npcs) n.gameObject.SetActive(false);
var cam = Vespertine.Core.Game.Cam.Cam;
var go = new UnityEngine.GameObject("kneecam"); var kc = go.AddComponent<UnityEngine.Camera>(); kc.CopyFrom(cam);
kc.cullingMask &= ~(1 << Vespertine.Core.Layers.Overlay);
var b = gl.transform.position;
go.transform.position = b + UnityEngine.Vector3.up * 22f; go.transform.rotation = UnityEngine.Quaternion.Euler(90, 0, 0);
kc.orthographic = true; kc.orthographicSize = c + 3f;
var rt = new UnityEngine.RenderTexture(800, 800, 24, UnityEngine.RenderTextureFormat.ARGBHalf); kc.targetTexture = rt; kc.Render();
UnityEngine.RenderTexture.active = rt; var tex = new UnityEngine.Texture2D(800, 800, UnityEngine.TextureFormat.RGBAHalf, false, true); tex.ReadPixels(new UnityEngine.Rect(0,0,800,800),0,0); tex.Apply(); UnityEngine.RenderTexture.active = null;
int bins = (int)((c + 2.5f) / 0.1f); var sum = new float[bins]; var cnt = new int[bins]; int dirs = 0;
for (int k = 0; k < 72; k++) {
  float a = k * 5f * UnityEngine.Mathf.Deg2Rad; var d = new UnityEngine.Vector3(UnityEngine.Mathf.Cos(a), 0, UnityEngine.Mathf.Sin(a));
  bool clear = true;
  for (float r = 0.2f; r < c + 2.5f; r += 0.25f) if (UnityEngine.Physics.Linecast(gl.SourcePos, b + d * r + UnityEngine.Vector3.up * 0.05f, Vespertine.Core.Layers.LightBlockMask, UnityEngine.QueryTriggerInteraction.Ignore) || UnityEngine.Physics.Raycast(b + d * r + UnityEngine.Vector3.up * 3f, UnityEngine.Vector3.down, 2.9f, Vespertine.Core.Layers.LightBlockMask)) { clear = false; break; }
  if (!clear) continue; dirs++;
  for (int i = 0; i < bins; i++) { var p = kc.WorldToViewportPoint(b + d * ((i + 0.5f) * 0.1f)); var col = tex.GetPixelBilinear(p.x, p.y); sum[i] += 0.2126f*col.r + 0.7152f*col.g + 0.0722f*col.b; cnt[i]++; }
}
kc.targetTexture = null; UnityEngine.Object.Destroy(go); rt.Release(); UnityEngine.Object.Destroy(tex);
foreach (var n in Vespertine.Core.Game.AI.Npcs) n.gameObject.SetActive(true);
if (before) { u.cookie = keep.ck; u.spotAngle = keep.a; u.innerSpotAngle = keep.i; u.range = keep.r; u.intensity = keep.it; }
if (dirs == 0) return "no clear directions";
var lum = new float[bins]; for (int i = 0; i < bins; i++) lum[i] = sum[i] / cnt[i];
System.Func<float, float, float> mean = (r0, r1) => { float s = 0; int m = 0; for (int i = 0; i < bins; i++) { float r = (i + 0.5f) * 0.1f; if (r >= r0 && r <= r1) { s += lum[i]; m++; } } return m > 0 ? s / m : 0; };
float centre = mean(0.6f, 1.2f), atC = mean(c - 0.6f, c - 0.2f), outside = mean(c + 0.9f, c + 2.0f);
float mid = (atC + outside) * 0.5f, rHalf = -1;
for (int i = 0; i < bins; i++) { float r = (i + 0.5f) * 0.1f; if (r > c - 1.5f && lum[i] < mid) { rHalf = r; break; } }
var sb = new System.Text.StringBuilder();
sb.Append(name + (before ? " BEFORE" : " KNEE") + " dirs=" + dirs + " contour=" + c.ToString("0.00") + " rHalf=" + rHalf.ToString("0.00") + " err=" + (rHalf - c - 0.15f).ToString("+0.00;-0.00") + " centre=" + centre.ToString("0.000") + " atContour=" + atC.ToString("0.000") + " outside=" + outside.ToString("0.000") + " contour/centre=" + (atC / centre).ToString("0.00") + " contour/outside=" + (atC / UnityEngine.Mathf.Max(1e-4f, outside)).ToString("0.0") + "\nprofile:");
for (int i = 0; i < bins; i += 3) sb.Append(" " + ((i + 0.5f) * 0.1f).ToString("0.0") + ":" + lum[i].ToString("0.000"));
return sb.ToString();
