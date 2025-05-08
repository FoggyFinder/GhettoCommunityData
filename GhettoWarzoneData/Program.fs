// https://www.falcoframework.com/docs/get-started.html
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open System.Threading
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.HttpOverrides
open System
open System.Collections.Generic
open Library

module Ui =
    open Falco.Markup
    
    let layout (title:string) (content : XmlNode list) =
        Elem.html [ Attr.lang "en"; ] [
            Elem.head [] [
                yield Elem.meta  [ Attr.charset "UTF-8" ]
                yield Elem.meta  [ Attr.httpEquiv "X-UA-Compatible"; Attr.content "IE=edge, chrome=1" ]
                yield Elem.meta  [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
            
                yield Elem.title [] [ Text.raw title ]
            
                yield Elem.link  [ Attr.href "/styles.css"; Attr.rel "stylesheet" ]

                yield Elem.link [ Attr.rel "shortcut icon"; Attr.href "/favicon.ico"; Attr.type' "image/x-icon" ]

                yield Elem.link [ Attr.rel "icon"; Attr.href "/favicon.ico"; Attr.type' "image/x-icon" ]
            ]

            Elem.body [ ] [ 
                Elem.header [ ] [
                    Elem.span [] [
                        Text.raw "Rewards pools info for "
                        Elem.a [ Attr.href $"https://warzones.ghettopigeon.com/"; Attr.targetBlank ] [
                            Text.raw $"GhettoWarzone Game" ]
                    ]
                ]
                Elem.main [] content
                Elem.footer [] [
                    Elem.a [ Attr.href "https://github.com/FoggyFinder/GhettoCommunityData"; Attr.targetBlank ]
                        [ Text.raw "Source code" ]
                ]
            ]
        ]

    let poolsTable(poolsOpt:RewardPoolRow list option) =
        [
            match poolsOpt with
            | Some pools ->
                let poolsTableHeader =
                    Elem.tr [] [
                        Elem.th [] [ Text.raw "Number" ]
                        Elem.th [] [ Text.raw "Asset" ]
                        Elem.th [] [ Text.raw "First place" ]
                        Elem.th [] [ Text.raw "Second place" ]
                        Elem.th [] [ Text.raw "Third place" ]
                        Elem.th [] [ Text.raw "Tokens Left" ]
                        Elem.th [] [ Text.raw "Games Left" ]
                    ]

                let sortedPoolsTableItems = 
                    pools
                    |> List.sortByDescending(fun pool -> pool.RewardPool.Reward.FstPlace * (pool.UsdPrice |> Option.defaultValue 0M))
                    |> List.mapi(fun i rpr ->
                        let asset = rpr.RewardPool.Asset
                        let sgr = rpr.RewardPool.Reward
                        [
                            Elem.tr [] [
                                Elem.td [] [ Text.raw $"{i + 1}" ]
                                Elem.td [] [ Text.raw $"{asset.Name} ({asset.Ticker})" ]
                                
                                Elem.td [] [ Text.raw $"{sgr.FstPlace} ({Utils.formatInUsd(sgr.FstPlace, rpr.UsdPrice)})" ]
                                Elem.td [] [ Text.raw $"{sgr.SndPlace} ({Utils.formatInUsd(sgr.SndPlace, rpr.UsdPrice)})" ]
                                Elem.td [] [ Text.raw $"{sgr.TrdPlace} ({Utils.formatInUsd(sgr.TrdPlace, rpr.UsdPrice)})" ]
                            
                                Elem.td [] [ Text.raw $"{rpr.RewardPool.PoolBalance} ({Utils.formatInUsd(rpr.RewardPool.PoolBalance, rpr.UsdPrice)})" ]
                                Elem.td [] [ Text.raw $"{rpr.RewardPool.FullGamesLeft}" ]
                            ]
                            Elem.tr [] [
                                Elem.td [ Attr.colspan "7" ] [ Text.raw $"{rpr.RewardPool.PurchaseLink}" ]
                            ]
                        ])
                    |> List.concat

                yield Elem.table [] [
                    yield poolsTableHeader
                    yield! sortedPoolsTableItems
                ]            
            
            | None ->
                yield Text.raw "API was changed or didn't return response. Try again later"
        ]

[<RequireQualifiedAccess>]
module Route =
    let [<Literal>] index = "/"
    let [<Literal>] pools = "/pools"

let endpoints =
    [
        get Route.index (fun ctx ->
            API.getPools()
            |> Ui.poolsTable
            |> Ui.layout "Home"
            |> fun html -> Response.ofHtml html ctx)
        
        get Route.pools (fun ctx ->
            API.getPools()
            |> Ui.poolsTable
            |> Ui.layout "Home"
            |> fun html -> Response.ofHtml html ctx)
    ]

let wapp = WebApplication.Create()

wapp.UseForwardedHeaders(ForwardedHeadersOptions(ForwardedHeaders = (ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto))) |> ignore
wapp.UseHsts() |> ignore

wapp.UseStaticFiles() |> ignore

wapp.UseRouting() |> ignore

wapp
    .UseFalco(endpoints)
    // ^-- activate Falco endpoint source
    .Run()