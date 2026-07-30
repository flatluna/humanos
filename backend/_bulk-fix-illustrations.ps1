$targets = @(
  @{ NodeId = "aa82d3a4-16c2-40a4-ac4f-e9c47140f5f0"; Step = "Hypothesis"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) mostrando el plano cartesiano de 4 cuadrantes con el punto Q(-4,-2) marcado pero SIN etiquetar a que cuadrante pertenece (solo las coordenadas del punto). No cambies el texto del paso." },
  @{ NodeId = "aa82d3a4-16c2-40a4-ac4f-e9c47140f5f0"; Step = "Teaching"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) mostrando el plano cartesiano de 4 cuadrantes con el punto Q(-4,-2) marcado y etiquetado, resaltando visualmente que esta en el cuadrante III (x negativo, y negativo). No cambies el texto del paso." },

  @{ NodeId = "3c3dee23-ce5a-417c-9763-d4b4b3441325"; Step = "Hypothesis"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) mostrando los puntos A(1,2) y B(5,5) marcados y etiquetados en el plano cartesiano, SIN mostrar la distancia final entre ellos ni el triangulo rectangulo. No cambies el texto del paso." },
  @{ NodeId = "3c3dee23-ce5a-417c-9763-d4b4b3441325"; Step = "Teaching"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) mostrando los puntos A(1,2) y B(5,5) marcados y etiquetados en el plano cartesiano exactamente en sus coordenadas reales. No cambies el texto del paso." },

  @{ NodeId = "495e6300-6b95-485f-8d74-fde17b686084"; Step = "Hypothesis"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Ellipse, CenterX=0, CenterY=0, RadiusX=5, RadiusY=2) mostrando la elipse centrada en el origen con semieje horizontal 5 y semieje vertical 2, SIN escribir la ecuacion completa x^2/25+y^2/4=1 en la imagen. No cambies el texto del paso." },
  @{ NodeId = "495e6300-6b95-485f-8d74-fde17b686084"; Step = "Teaching"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Ellipse, CenterX=0, CenterY=0, RadiusX=5, RadiusY=2, EquationLabel='x^2/25 + y^2/4 = 1') mostrando la elipse centrada en el origen con semieje mayor 5 y semieje menor 2. No cambies el texto del paso." },

  @{ NodeId = "f113029a-1749-4b11-991a-e3549d763fa2"; Step = "Teaching"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) marcando y etiquetando los puntos (1,2), (2,5) y (3,7) del conjunto que SI es funcion, y ademas los puntos (1,2) y (1,4) del conjunto que NO es funcion (mismo x=1 con dos valores y distintos), todos claramente etiquetados con sus coordenadas exactas. No cambies el texto del paso." },

  @{ NodeId = "7bb4970c-2b90-4605-864a-a10473ef8395"; Step = "Hypothesis"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) mostrando el punto (3,2) marcado en el plano cartesiano pero SIN etiquetar el punto con su nombre/coordenadas (solo el punto visible). No cambies el texto del paso." },
  @{ NodeId = "7bb4970c-2b90-4605-864a-a10473ef8395"; Step = "Teaching"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) mostrando el punto A(3,2) marcado y claramente etiquetado con sus coordenadas exactas en el plano cartesiano. No cambies el texto del paso." },

  @{ NodeId = "143754e9-28e1-4b2e-ad79-12f0a20ddf97"; Step = "Hypothesis"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto usando Kind=Circle con CenterX=0, CenterY=0, Radius=5, y VerticalLineTestX=3 (la recta vertical de prueba en x=3 cruza el circulo en (3,4) y (3,-4), un triangulo 3-4-5). Usa un circulo (no una funcion como una parabola o cubica, porque esas SI pasan la prueba de la recta vertical) para mostrar una curva que la recta vertical corta en dos puntos, sin explicar el resultado en la imagen. No cambies el texto del paso." },
  @{ NodeId = "143754e9-28e1-4b2e-ad79-12f0a20ddf97"; Step = "Teaching"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto usando Kind=Circle con CenterX=0, CenterY=0, Radius=5, y VerticalLineTestX=3 (la recta vertical de prueba en x=3 cruza el circulo en (3,4) y (3,-4)). Usa un circulo (no una funcion, porque una funcion real siempre pasa la prueba de la recta vertical) para demostrar que NO representa una funcion, ya que la recta vertical corta la curva en dos puntos resaltados con sus coordenadas exactas. No cambies el texto del paso." },

  @{ NodeId = "0069a299-b1a1-4f69-87ce-006bbb3fc27d"; Step = "Hypothesis"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) mostrando marcas/lineas guia hacia x=-3 y y=2 en el plano cartesiano, pero SIN el punto final P(-3,2) marcado (para que el estudiante infiera donde queda). No cambies el texto del paso." },
  @{ NodeId = "0069a299-b1a1-4f69-87ce-006bbb3fc27d"; Step = "Teaching"; Instr = "Corrige la ilustracion para que sea un MathDiagram deterministico exacto (Kind=Points) mostrando el punto P(-3,2) marcado y claramente etiquetado con sus coordenadas exactas en el plano cartesiano. No cambies el texto del paso." }
)

$results = New-Object System.Collections.Generic.List[object]
$i = 0
foreach ($t in $targets) {
  $i++
  Write-Host "[$i/$($targets.Count)] $($t.NodeId) / $($t.Step) ..."
  $body = @{ Instruction = $t.Instr } | ConvertTo-Json
  try {
    $resp = Invoke-RestMethod -Uri "http://localhost:7071/api/studio/nodes/$($t.NodeId)/blueprint/steps/$($t.Step)/edit" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 180
    $illustrationId = $resp.illustrations[0].illustrationId
    Write-Host "    OK -> illustrationId=$illustrationId"
    $results.Add([pscustomobject]@{ NodeId = $t.NodeId; Step = $t.Step; Status = "OK"; IllustrationId = $illustrationId; Error = $null })
  } catch {
    $errMsg = $_.Exception.Message
    if ($_.ErrorDetails) { $errMsg = $_.ErrorDetails.Message }
    Write-Host "    ERROR -> $errMsg"
    $results.Add([pscustomobject]@{ NodeId = $t.NodeId; Step = $t.Step; Status = "ERROR"; IllustrationId = $null; Error = $errMsg })
  }
}

$results | ConvertTo-Json -Depth 5 | Out-File -FilePath "$env:TEMP\bulk-fix-results.json" -Encoding utf8
Write-Host "`nDONE. Results saved."
