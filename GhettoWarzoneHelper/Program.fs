﻿// For more information see https://aka.ms/fsharp-console-apps
namespace Library
open System
open System.Net.Http
open Newtonsoft.Json.Linq

type Asset = {
    Id: uint64
    Ticker: string
    Name: string
}

type SingleGameReward = {
    FstPlace: decimal
    SndPlace: decimal
    TrdPlace: decimal
}

type RewardPool = {
    Asset: Asset
    PoolBalance: decimal
    Reward: SingleGameReward
    PurchaseLink: string
} with
    member rp.FullGamesLeft =
        Math.Floor(rp.PoolBalance / (rp.Reward.FstPlace + rp.Reward.SndPlace + rp.Reward.TrdPlace)) |> int

type RewardPoolRow(rp:RewardPool, usdPrice: decimal option) =
    member _.RewardPool = rp
    member _.UsdPrice = usdPrice

[<RequireQualifiedAccess>]
module Utils =
    open System
    open Newtonsoft.Json.Linq

    let rewardPoolsFromJson (content:string) =
        try
            let jObj = JObject.Parse(content)
            let pools = 
                jObj.SelectToken("pools").Values()
                |> Seq.map(fun (jToken:JToken) ->
                    let assetId = jToken.SelectToken("tokenID").Value<uint64>()
                    let assetName = jToken.SelectToken("tokenName").Value<string>()
                    let assetTicker = jToken.SelectToken("unitName").Value<string>()
                    let purchaseLink = jToken.SelectToken("purchaseLink").Value<string>()
                    let tokenInPool = jToken.SelectToken("amountLeft").Value<decimal>()

                    let prizes = jToken.SelectToken("prizes")

                    let first = prizes.SelectToken("firstPlace").Value<decimal>()
                    let second = prizes.SelectToken("secondPlace").Value<decimal>()
                    let third = prizes.SelectToken("thirdPlace").Value<decimal>()
                    {
                        Asset = {
                            Id = assetId
                            Ticker = assetTicker
                            Name = assetName                        
                        }
                        PurchaseLink = purchaseLink
                        PoolBalance = tokenInPool
                        Reward = {
                            FstPlace = first
                            SndPlace = second
                            TrdPlace = third
                        }
                    })
                |> Seq.toList
            pools |> Some
        with e ->
            printfn "%A" e
            None
    let round7 (v:decimal) = Math.Round(v, 7)

    let formatInUsd(asset:decimal, usd: decimal option) =
        usd |> Option.map((*) asset >> round7 >> string) |> Option.defaultValue "-"

[<RequireQualifiedAccess>]
module API =
    let getUsdPrice(assetId: uint64) =
        let client = new HttpClient()
        async {
            try
                let uri = $"https://free-api.vestige.fi/asset/{assetId}/price?currency=usd"
                use request = new System.Net.Http.HttpRequestMessage()
                request.Method <- System.Net.Http.HttpMethod.Get
                request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"))
                request.RequestUri <- System.Uri(uri)
                let! response = client.SendAsync(request) |> Async.AwaitTask
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                let jObj = JObject.Parse(content)
                return jObj.SelectToken("price").Value<decimal>() |> Some
            with exp ->
                return None
        } |> Async.RunSynchronously

    let getPools() =
        let client = new HttpClient()
        async {
            try
                let uri = "https://api.ghettopigeon.com/api/v1/game-pools"
                use request = new System.Net.Http.HttpRequestMessage()
                request.Method <- System.Net.Http.HttpMethod.Get
                request.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"))
                request.RequestUri <- System.Uri(uri)
                let! response = client.SendAsync(request) |> Async.AwaitTask
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                return
                    Utils.rewardPoolsFromJson content
                    |> Option.map(fun pools ->
                        pools |> List.map(fun rp -> RewardPoolRow(rp, getUsdPrice rp.Asset.Id)))
            with exp ->
                return None
        } |> Async.RunSynchronously