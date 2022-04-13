[datetime] $start=[datetime]::new(2021,2,1)
[datetime] $end=[datetime]::Now
[datetime] $date = $start

while($date -lt $end) {
    Invoke-WebRequest "https://api.polygon.io/v2/aggs/ticker/SPGI/range/1/minute/$($date.ToString("yyyy-MM-dd"))/$($date.ToString("yyyy-MM-dd"))?adjusted=true&sort=asc&limit=50000&apiKey=OwSNQI92QgWOjuW2N1KLiuy0y09NKYTw" -OutFile "$PSScriptRoot/Data/SPGI-$($date.ToString("yyyy-MM-dd")).json"
    $date = $date.AddDays(1)
   
}