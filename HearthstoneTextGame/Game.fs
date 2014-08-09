﻿namespace HearthstoneTextGame

open System

module Game =

    EntityJson.preload()

    let initPlayer name =
        { Player.Empty with Name = name
                            HeroClass = "Mage"
                            HeroPower = Hero.getHeroPower "Mage" true
                            Deck = Deck.getRandomDeck "Mage" }

    let initPlayerWithDeck name deck = 
        { Player.Empty with Name = name
                            HeroClass = deck.DeckClass
                            HeroPower = Hero.getHeroPower deck.DeckClass true
                            Deck = deck }               

    let getPlayer (playerGuid : string) (game : GameSession) =
        let result = game.Players |> List.filter(fun e -> e.Guid = playerGuid)
        match result |> List.isEmpty with
        | true -> None
        | false -> Some (result |> List.head)

    let getOpponent (playerGuid : string) (game : GameSession) =
        let result = game.Players |> List.filter(fun e -> e.Guid <> playerGuid)
        match result |> List.isEmpty with
        | true -> None
        | false -> Some (result |> List.head)

    let addPlayer (player : Player) (game : GameSession) =
        if game.PlayerCount = 2 then None
        else Some { game with Players = player :: game.Players }

    let registerPlayer (playerName : string) (deck : Deck) (game : GameSession) =
        let newPlayer = initPlayerWithDeck playerName deck
        addPlayer newPlayer game |> Option.map(fun newGame -> newPlayer, newGame)

    let registerRandomDeckPlayer (playerName : string) (game : GameSession) =
        let newPlayer = initPlayer playerName
        addPlayer newPlayer game |> Option.map(fun newGame -> newPlayer, newGame)

    let registerRandomDeckPlayerWithClass (playerName : string) (playerClass : string) (game : GameSession) =
        let deck = Deck.getRandomDeck(playerClass)
        registerPlayer playerName deck game

    let updatePlayer (items : ICharacter list) (player : Player) =
        let newPlayer = ref player
        items
        |> List.iter(fun item ->
            if item.Guid = player.HeroCharacter.Guid then
                newPlayer := { !newPlayer with HeroCharacter = (item :?> HeroCharacter) }
            else
                let newMinionPosition =
                    player.MinionPosition |> List.map(fun minion ->
                        if item.Guid = minion.Guid then (item :?> Minion) else minion)
                newPlayer := { !newPlayer with MinionPosition = newMinionPosition })
        !newPlayer

    let updatePlayerToGame (player : Player) (game : GameSession) =
        let newPlayers =
            game.Players |> List.map (fun e ->
                if e.Guid = player.Guid then player
                else e)
        { game with Players = newPlayers }

    let updateICharToGame (items : ICharacter list) (game : GameSession) =
        let newPlayers = game.Players |> List.map (fun e -> updatePlayer items e)
        { game with Players = newPlayers }

    let playMinion (minion : Minion) (pos : int) (player : Player) (game : GameSession) =
        if pos > (player.MinionPosition |> List.length) || pos < 0 then
            None
        else 
            let newMinionPosition = Utility.insert minion pos player.MinionPosition
            let newPlayer = { player with MinionPosition = newMinionPosition }
            Some <| updatePlayerToGame newPlayer game

    let playWeapon (weapon : Weapon) (player : Player) (game : GameSession) =
        let newAttackValue = player.HeroCharacter.AttackValue + weapon.Attack
        let newPlayer = { player with ActiveWeapon = Some weapon; HeroCharacter = { player.HeroCharacter with AttackValue = newAttackValue } }
        Some <| updatePlayerToGame newPlayer game

    let drawCard (player : Player) (game : GameSession) =
        if player.Deck.RemainingCardsCount > 0 then  
            let cardDraw, remainDeck = Deck.drawCardFromDeck player.Deck
            let cardDrawOnHand = { Cost = cardDraw.Cost.Value; Card = cardDraw }
            let newHand = Utility.insert cardDrawOnHand (player.Hand |> List.length) player.Hand
            let newPlayer = { player with Hand = newHand; Deck = remainDeck }
            let newGame = updatePlayerToGame newPlayer game
            Some cardDraw, newGame
        else
            None, game

    let useHeroPower (player : Player) (target : ICharacter option) (game : GameSession) =
        if player.CurrentMana < 2 then None
        else if player.HeroPowerUsed then None
        else
            let newPlayer = { player with HeroPowerUsed = true; CurrentMana = player.CurrentMana - 2 }
            let newGame = updatePlayerToGame newPlayer game
            match player.HeroPower.Id with
            | "CS2_034" (* Fireblast *) ->
                let newTarget = target.Value.GetDamage(1)
                Some <| updateICharToGame [newTarget] newGame
            | "CS2_017" (* Shapeshift *) ->
                let armour = newPlayer.HeroCharacter.Armour + 1
                let attackVal = newPlayer.HeroCharacter.AttackValue + 1
                let newHeroChar = { newPlayer.HeroCharacter with Armour = armour; AttackValue = attackVal }
                let aPlayer = newPlayer |> updatePlayer [newHeroChar]
                Some <| updatePlayerToGame aPlayer newGame
            | "CS2_049" (* Totemic Call *) ->
                let totem = Minion.Parse(Card.getRandomTotem())
                if totem.IsNone then None
                else playMinion totem.Value (newPlayer.MinionPosition |> List.length) newPlayer newGame          
            | "DS1h_292" (* Steady Shot *) ->
                let opponent = getOpponent newPlayer.Guid newGame
                if opponent.IsNone then None
                else
                    let newTarget = (opponent.Value.HeroCharacter :> ICharacter).GetDamage(2)
                    let aPlayer = opponent.Value |> updatePlayer [newTarget]
                    Some <| updatePlayerToGame aPlayer newGame
            | "CS2_101" (* Reinforce *) ->
                let card = Card.getCardByExactName("Silver Hand Recruit")
                let token = Minion.Parse(card)
                if token.IsNone then None
                else playMinion token.Value (newPlayer.MinionPosition |> List.length) newPlayer newGame
            | "CS2_083b" (* Dagger Mastery *) ->
                let knife = Weapon.Parse(Card.getCardByExactName("Wicked Knife"))
                if knife.IsNone then None
                else playWeapon knife.Value newPlayer newGame
            | "CS1h_001" (* Lesser Heal *) ->
                let newTarget = target.Value.GetHeal(2)
                Some <| updateICharToGame [newTarget] newGame
            | "CS2_056" (* Life Tap *) ->
                let _, newGame = drawCard newPlayer newGame
                let newHeroCharacter = (newPlayer.HeroCharacter :> ICharacter).GetDamage(2) :?> HeroCharacter
                Some <| updateICharToGame [newHeroCharacter] newGame
            | "CS2_102" (* Armour Up! *) ->
                let newArmour = newPlayer.HeroCharacter.Armour + 2
                let newHeroChar = { newPlayer.HeroCharacter with Armour = newArmour }
                let aPlayer = newPlayer |> updatePlayer [newHeroChar]
                Some <| updatePlayerToGame aPlayer newGame
            | _ ->
                None

    let findIChar (guid : string) (game : GameSession) ifHero ifMinion =
        let found = 
            game.Players 
            |> List.choose (fun e ->
                if e.HeroCharacter.Guid = guid then
                    ifHero e.HeroCharacter
                    Some (e.HeroCharacter :> ICharacter)
                else
                    e.MinionPosition |> List.tryFind (fun m -> m.Guid = guid)
                    |> Option.map(fun minion ->
                        ifMinion minion
                        minion :> ICharacter
                    )
            )

        if found |> List.length = 1 then Some found.Head
        else None

    let findTargetForHeroPower (player : Player) (game : GameSession) =
        Hero.heroPowers |> List.tryFind(fun e -> e = player.HeroPower)
        |> Option.bind(fun heroPower ->
            heroPower.Target |> Option.bind(fun target -> 
                getOpponent player.Guid game |> Option.map(fun opponent ->
                    match target with
                    | AnyTarget Any ->
                        [ player.HeroCharacter.Guid
                          opponent.HeroCharacter.Guid ]
                        |> List.append(player.MinionPosition |> List.map(fun e -> e.Guid))
                        |> List.append(opponent.MinionPosition |> List.map(fun e -> e.Guid))
                    | AnyTarget Friendly ->
                        [ player.HeroCharacter.Guid ]
                        |> List.append(player.MinionPosition |> List.map(fun e -> e.Guid))
                    | AnyTarget Enemy ->
                        [ opponent.HeroCharacter.Guid ]
                        |> List.append(opponent.MinionPosition |> List.map(fun e -> e.Guid))
                    | MinionTarget Any ->
                        [ ]
                        |> List.append(player.MinionPosition |> List.map(fun e -> e.Guid))
                        |> List.append(opponent.MinionPosition |> List.map(fun e -> e.Guid))
                    | MinionTarget Friendly ->
                        [ ]
                        |> List.append(player.MinionPosition |> List.map(fun e -> e.Guid))
                    | MinionTarget Enemy ->
                        [ ]
                        |> List.append(opponent.MinionPosition |> List.map(fun e -> e.Guid))
                    )
                )
            )

    let startGame (game : GameSession) =
        if game.PlayerCount <> 2 || game.CurrentPhase <> NotStarted then None
        else
            let startPlayer = Random().Next(2) |> List.nth game.Players
            let secondPlayer = (getOpponent startPlayer.Guid game).Value
            let newSecondPlayer = { secondPlayer with Hand = { Cost = 0; Card = Card.TheCoin } :: secondPlayer.Hand}
            let newGame = updatePlayerToGame newSecondPlayer game
            let _, newGameWithDrawCard = drawCard startPlayer newGame
            Some { newGameWithDrawCard with ActivePlayerGuid = startPlayer.Guid; CurrentPhase = Playing }

            

    let endTurn (game : GameSession) =
        if game.CurrentPhase <> Playing then None
        else
            let mutable newGame = game
            // Activate trigger in endTurn of current Player
            // DO


            // Reset hero power
            let cPlayer = (getPlayer game.ActivePlayerGuid game).Value
            let cPlayerTemp = { cPlayer with HeroPowerUsed = false }
            newGame <- updatePlayerToGame cPlayerTemp newGame
         
            // Set activePlayer to opponent and go to next phase
            let opp = (getOpponent game.ActivePlayerGuid game).Value
            newGame <- { game with ActivePlayerGuid = opp.Guid }


            // Activate trigger in beginTurn of Opp
            // DO
        
            // Draw card
            let _, aGame = drawCard (getPlayer newGame.ActivePlayerGuid newGame).Value newGame
            newGame <- aGame

            // Increase mana for ActivePlayer
            let tempPlayer = (getPlayer newGame.ActivePlayerGuid newGame).Value
            let maxMana = if tempPlayer.MaxMana = 10 then 10 else tempPlayer.MaxMana + 1
            let newPlayer = { tempPlayer with MaxMana = maxMana
                                              CurrentMana = maxMana }

            // Set player and go to next phase
            Some <| updatePlayerToGame newPlayer newGame