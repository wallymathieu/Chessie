/// Contains error propagation functions and a computation expression builder for Railway-oriented programming.
namespace Chessie.ErrorHandling

open System
[<AutoOpen>]
module FSharpCore=
    type Result<'TSuccess, 'TError> = 
         /// Represents the result of a successful computation.
        | Ok of 'TSuccess
         /// Represents the result of a failed computation.
        | Error of 'TError
type RopResult<'TSuccess, 'TMessage> = FSharpCore.Result<'TSuccess * 'TMessage list , 'TMessage list>
/// Represents the result of a computation.
type Result<'TSuccess, 'TMessage> = 
    static member Ok(value:'TSuccess) : RopResult<'TSuccess, 'TMessage> = Ok(value,[])
    static member Bad(value:'TMessage list) : RopResult<'TSuccess, 'TMessage> = Error(value)

    /// Creates a Failure result with the given messages.
    static member FailWith(messages:'TMessage seq) : RopResult<'TSuccess, 'TMessage> = Error(messages |> Seq.toList)

    /// Creates a Failure result with the given message.
    static member FailWith(message:'TMessage) : RopResult<'TSuccess, 'TMessage> = Error([message])
    
    /// Creates a Success result with the given value.
    static member Succeed(value:'TSuccess) : RopResult<'TSuccess, 'TMessage> = Ok(value,[])

    /// Creates a Success result with the given value and the given message.
    static member Succeed(value:'TSuccess,message:'TMessage) : RopResult<'TSuccess, 'TMessage> = Ok(value,[message])

    /// Creates a Success result with the given value and the given message.
    static member Succeed(value:'TSuccess,messages:'TMessage seq) : RopResult<'TSuccess, 'TMessage> = Ok(value,messages |> Seq.toList)

    /// Executes the given function on a given success or captures the failure
    static member Try(func: Func<'TSuccess>) : RopResult<'TSuccess,exn> =        
        try
            Ok(func.Invoke(),[])
        with
        | exn -> Error([exn])

    /// Converts the result into a string.
    static member ToString(r:RopResult<'TSuccess, 'TMessage>) =
        match r with
        | Ok(v,msgs) -> sprintf "OK: %A - %s" v (String.Join(Environment.NewLine, msgs |> Seq.map (fun x -> x.ToString())))
        | Error(msgs) -> sprintf "Error: %s" (String.Join(Environment.NewLine, msgs |> Seq.map (fun x -> x.ToString())))    

/// Basic combinators and operators for error handling.
[<AutoOpen>]
module Trial =  
    /// Wraps a value in a Success
    let inline ok<'TSuccess,'TMessage> (x:'TSuccess) : RopResult<'TSuccess,'TMessage> = Ok((x, []))

    /// Wraps a value in a Success
    let inline pass<'TSuccess,'TMessage> (x:'TSuccess) : RopResult<'TSuccess,'TMessage> = Ok(x, [])

    /// Wraps a value in a Success and adds a message
    let inline warn<'TSuccess,'TMessage> (msg:'TMessage) (x:'TSuccess) : RopResult<'TSuccess,'TMessage> = Ok(x,[msg])

    /// Wraps a message in a Failure
    let inline fail<'TSuccess,'Message> (msg:'Message) : RopResult<'TSuccess,'Message> = Error([ msg ])

    /// Executes the given function on a given success or captures the exception in a failure
    let inline Catch (f:'a->'b) (x:'a) = Result.Try(fun () -> f x)

    /// Returns true if the result was not successful.
    let inline failed result = 
        match result with
        | Error _ -> true
        | _ -> false

    /// Takes a Result and maps it with fSuccess if it is a Success otherwise it maps it with fFailure.
    let inline either fSuccess fFailure trialResult = 
        match trialResult with
        | Ok(x, msgs) -> fSuccess (x, msgs)
        | Error(msgs) -> fFailure (msgs)

    /// If the given result is a Success the wrapped value will be returned. 
    ///Otherwise the function throws an exception with Failure message of the result.
    let inline returnOrFail result = 
        let inline raiseExn msgs = 
            msgs
            |> Seq.map (sprintf "%O")
            |> String.concat (Environment.NewLine + "\t")
            |> failwith
        either fst raiseExn result

    /// Appends the given messages with the messages in the given result.
    let inline mergeMessages msgs result = 
        let inline fSuccess (x, msgs2) = Ok(x, msgs @ msgs2)
        let inline fFailure errs = Error(errs @ msgs)
        either fSuccess fFailure result

    /// If the result is a Success it executes the given function on the value.
    /// Otherwise the exisiting failure is propagated.
    let inline bind f result = 
        let inline fSuccess (x, msgs) = f x |> mergeMessages msgs
        let inline fFailure (msgs) = Error msgs
        either fSuccess fFailure result

   /// Flattens a nested result given the Failure types are equal
    let inline flatten (result : RopResult<RopResult<_,_>,_>) =
        result |> bind id

    /// If the result is a Success it executes the given function on the value. 
    /// Otherwise the exisiting failure is propagated.
    /// This is the infix operator version of ErrorHandling.bind
    let inline (>>=) result f = bind f result

    /// If the wrapped function is a success and the given result is a success the function is applied on the value. 
    /// Otherwise the exisiting error messages are propagated.
    let inline apply wrappedFunction result = 
        match wrappedFunction, result with
        | Ok(f, msgs1), Ok(x, msgs2) -> Ok(f x, msgs1 @ msgs2)
        | Error errs, Ok(_, _msgs) -> Error(errs)
        | Ok(_, _msgs), Error errs -> Error(errs)
        | Error errs1, Error errs2 -> Error(errs1 @ errs2)

    /// If the wrapped function is a success and the given result is a success the function is applied on the value. 
    /// Otherwise the exisiting error messages are propagated.
    /// This is the infix operator version of ErrorHandling.apply
    let inline (<*>) wrappedFunction result = apply wrappedFunction result

    /// Lifts a function into a Result container and applies it on the given result.
    let inline lift f result = apply (ok f) result

    /// Maps a function over the existing error messages in case of failure. In case of success, the message type will be changed and warnings will be discarded.
    let inline mapFailure f result =
        match result with
        | Ok (v,_) -> ok v
        | Error errs -> Error (f errs)

    /// Lifts a function into a Result and applies it on the given result.
    /// This is the infix operator version of ErrorHandling.lift
    let inline (<!>) f result = lift f result

    /// Promote a function to a monad/applicative, scanning the monadic/applicative arguments from left to right.
    let inline lift2 f a b = f <!> a <*> b

    /// If the result is a Success it executes the given success function on the value and the messages.
    /// If the result is a Failure it executes the given failure function on the messages.
    /// Result is propagated unchanged.
    let inline eitherTee fSuccess fFailure result =
        let inline tee f x = f x; x;
        tee (either fSuccess fFailure) result

    /// If the result is a Success it executes the given function on the value and the messages.
    /// Result is propagated unchanged.
    let inline successTee f result = 
        eitherTee f ignore result

    /// If the result is a Failure it executes the given function on the messages.
    /// Result is propagated unchanged.
    let inline failureTee f result = 
        eitherTee ignore f result

    /// Collects a sequence of Results and accumulates their values.
    /// If the sequence contains an error the error will be propagated.
    let inline collect xs = 
        Seq.fold (fun result next -> 
            match result, next with
            | Ok(rs, m1), Ok(r, m2) -> Ok(r :: rs, m1 @ m2)
            | Ok(_, m1), Error(m2) | Error(m1), Ok(_, m2) -> Error(m1 @ m2)
            | Error(m1), Error(m2) -> Error(m1 @ m2)) (ok []) xs
        |> lift List.rev

    /// Converts an option into a Result.
    let inline failIfNone message result = 
        match result with
        | Some x -> ok x
        | None -> fail message

    /// Converts a Choice into a Result.
    let inline ofChoice choice =
        match choice with
        | Choice1Of2 v -> ok v
        | Choice2Of2 v -> fail v

    /// Categorizes a result based on its state and the presence of extra messages
    let inline (|Pass|Warn|Fail|) result =
      match result with
      | Ok  (value, []  ) -> Pass  value
      | Ok  (value, msgs) -> Warn (value,msgs)
      | Error        msgs  -> Fail        msgs

    let inline failOnWarnings result =
      match result with
      | Warn (_,msgs) -> Error msgs
      | _             -> result 

    /// Builder type for error handling computation expressions.
    type TrialBuilder() = 
        member __.Zero() = ok()
        member __.Bind(m, f) = bind f m
        member __.Return(x) = ok x
        member __.ReturnFrom(x) = x
        member __.Combine (a, b) = bind b a
        member __.Delay f = f
        member __.Run f = f ()
        member __.TryWith (body, handler) =
            try
                body()
            with
            | e -> handler e
        member __.TryFinally (body, compensation) =
            try
                body()
            finally
                compensation()
        member x.Using(d:#IDisposable, body) =
            let result = fun () -> body d
            x.TryFinally (result, fun () ->
                match d with
                | null -> ()
                | d -> d.Dispose())
        member x.While (guard, body) =
            if not <| guard () then
                x.Zero()
            else
                bind (fun () -> x.While(guard, body)) (body())
        member x.For(s:seq<_>, body) =
            x.Using(s.GetEnumerator(), fun enum ->
                x.While(enum.MoveNext,
                    x.Delay(fun () -> body enum.Current)))

    /// Wraps computations in an error handling computation expression.
    let trial = TrialBuilder()

/// Represents the result of an async computation
[<NoComparison;NoEquality>]
type AsyncResult<'a, 'b> = 
    | AR of Async<RopResult<'a, 'b>>

/// Useful functions for combining error handling computations with async computations.
[<AutoOpen>]
module AsyncExtensions = 
    /// Useful functions for combining error handling computations with async computations.
    [<RequireQualifiedAccess>]
    module Async = 
        /// Creates an async computation that return the given value
        let singleton value = value |> async.Return

        /// Creates an async computation that runs a computation and
        /// when it generates a result run a binding function on the said result
        let bind f x = async.Bind(x, f)

        /// Creates an async computation that runs a mapping function on the result of an async computation
        let map f x = x |> bind (f >> singleton)

        /// Creates an async computation from an asyncTrial computation
        let ofAsyncResult (AR x) = x

/// Basic support for async error handling computation
[<AutoOpen>]
module AsyncTrial = 
    /// Builder type for error handling in async computation expressions.
    type AsyncTrialBuilder() = 
        member __.Return value : AsyncResult<'a, 'b> = 
            value
            |> ok
            |> Async.singleton
            |> AR
        
        member __.ReturnFrom(asyncResult : AsyncResult<'a, 'b>) = asyncResult
        member this.Zero() : AsyncResult<unit, 'b> = this.Return()
        member __.Delay(generator : unit -> AsyncResult<'a, 'b>) : AsyncResult<'a, 'b> = 
            async.Delay(generator >> Async.ofAsyncResult) |> AR
        
        member __.Bind(asyncResult : AsyncResult<'a, 'c>, binder : 'a -> AsyncResult<'b, 'c>) : AsyncResult<'b, 'c> = 
            let fSuccess (value, msgs) = 
                value |> (binder
                          >> Async.ofAsyncResult
                          >> Async.map (mergeMessages msgs))
            
            let fFailure errs = 
                errs
                |> Error
                |> Async.singleton
            
            asyncResult
            |> Async.ofAsyncResult
            |> Async.bind (either fSuccess fFailure)
            |> AR
        
        member this.Bind(result : RopResult<'a, 'c>, binder : 'a -> AsyncResult<'b, 'c>) : AsyncResult<'b, 'c> = 
            this.Bind(result
                      |> Async.singleton
                      |> AR, binder)
        
        member __.Bind(async : Async<'a>, binder : 'a -> AsyncResult<'b, 'c>) : AsyncResult<'b, 'c> = 
            async
            |> Async.bind (binder >> Async.ofAsyncResult)
            |> AR
        
        member __.TryWith(asyncResult : AsyncResult<'a, 'b>, catchHandler : exn -> AsyncResult<'a, 'b>) : AsyncResult<'a, 'b> = 
            async.TryWith(asyncResult |> Async.ofAsyncResult, (catchHandler >> Async.ofAsyncResult)) |> AR
        member __.TryFinally(asyncResult : AsyncResult<'a, 'b>, compensation : unit -> unit) : AsyncResult<'a, 'b> = 
            async.TryFinally(asyncResult |> Async.ofAsyncResult, compensation) |> AR
        member __.Using(resource : 'T when 'T :> System.IDisposable, binder : 'T -> AsyncResult<'a, 'b>) : AsyncResult<'a, 'b> = 
            async.Using(resource, (binder >> Async.ofAsyncResult)) |> AR
    
    // Wraps async computations in an error handling computation expression.
    let asyncTrial = AsyncTrialBuilder()

namespace Chessie.ErrorHandling.CSharp

open System
open System.Runtime.CompilerServices
open Chessie.ErrorHandling
open FSharpCore
/// Extensions methods for easier C# usage.
[<Extension>]
type ResultExtensions () =
    /// Allows pattern matching on Results from C#.
    [<Extension>]
    static member inline Match(this, ifSuccess:Action<'TSuccess , ('TMessage list)>, ifFailure:Action<'TMessage list>) =
        match this with
        | FSharpCore.Ok(x, msgs) -> ifSuccess.Invoke(x,msgs)
        | FSharpCore.Error(msgs) -> ifFailure.Invoke(msgs)
    
    /// Allows pattern matching on Results from C#.
    [<Extension>]
    static member inline Either(this, ifSuccess:Func<'TSuccess , ('TMessage list),'TResult>, ifFailure:Func<'TMessage list,'TResult>) =
        match this with
        | Result.Ok(x, msgs) -> ifSuccess.Invoke(x,msgs)
        | Result.Error(msgs) -> ifFailure.Invoke(msgs)

    /// Lifts a Func into a Result and applies it on the given result.
    [<Extension>]
    static member inline Map(this:RopResult<'TSuccess, 'TMessage>,func:Func<_,_>) =
        lift func.Invoke this

    /// Collects a sequence of Results and accumulates their values.
    /// If the sequence contains an error the error will be propagated.
    [<Extension>]
    static member inline Collect(values:seq<RopResult<'TSuccess, 'TMessage>>) =
        collect values

    /// Collects a sequence of Results and accumulates their values.
    /// If the sequence contains an error the error will be propagated.
    [<Extension>]
    static member inline Flatten(this) : RopResult<seq<'TSuccess>,'TMessage>=
        match this with
        | RopResult.Ok(values:RopResult<'TSuccess,'TMessage> seq, _msgs:'TMessage list) -> 
            match collect values with
            | RopResult.Ok(values,msgs) -> Ok(values |> List.toSeq,msgs)
            | RopResult.Error(msgs:'TMessage list) -> Error msgs
        | RopResult.Error(msgs:'TMessage list) -> Error msgs

    /// If the result is a Success it executes the given Func on the value.
    /// Otherwise the exisiting failure is propagated.
    [<Extension>]
    static member inline SelectMany (this:RopResult<'TSuccess, 'TMessage>, func: Func<_,_>) =
        bind func.Invoke this

    /// If the result is a Success it executes the given Func on the value.
    /// If the result of the Func is a Success it maps it using the given Func.
    /// Otherwise the exisiting failure is propagated.
    [<Extension>]
    static member inline SelectMany (this:RopResult<'TSuccess, 'TMessage>, func: Func<_,_>, mapper: Func<_,_,_>) =
        bind (fun s -> s |> func.Invoke |> lift (fun v -> mapper.Invoke(s,v))) this

    /// Lifts a Func into a Result and applies it on the given result.
    [<Extension>]
    static member inline Select (this:RopResult<'TSuccess, 'TMessage>, func: Func<_,_>) = lift func.Invoke this

    /// Returns the error messages or fails if the result was a success.
    [<Extension>]
    static member inline FailedWith(this:RopResult<'TSuccess, 'TMessage>) = 
        match this with
        | Result.Ok(v,msgs) -> failwithf "Result was a success: %A - %s" v (String.Join(Environment.NewLine, msgs |> Seq.map (fun x -> x.ToString())))
        | Result.Error(msgs) -> msgs

    /// Returns the result or fails if the result was an error.
    [<Extension>]
    static member inline SucceededWith(this:RopResult<'TSuccess, 'TMessage>) : 'TSuccess = 
        match this with
        | Result.Ok(v,_msgs) -> v
        | Result.Error(msgs) -> failwithf "Result was an error: %s" (String.Join(Environment.NewLine, msgs |> Seq.map (fun x -> x.ToString())))

    /// Joins two results. 
    /// If both are a success the resultSelector Func is applied to the values and the existing success messages are propagated.
    /// Otherwise the exisiting error messages are propagated.
    [<Extension>]
    static member inline Join (this: RopResult<'TOuter, 'TMessage>, inner: RopResult<'TInner, 'TMessage>, _outerKeySelector: Func<'TOuter,'TKey>, _innerKeySelector: Func<'TInner, 'TKey>, resultSelector: Func<'TOuter, 'TInner, 'TResult>) =
        let curry func = fun a b -> func (a, b)
        curry resultSelector.Invoke
        <!> this 
        <*> inner

    /// Converts an option into a Result.
    [<Extension>]
    static member ToResult(this, msg) =
        this |> failIfNone msg

    /// Maps a function over the existing error messages in case of failure. In case of success, the message type will be changed and warnings will be discarded.
    [<Extension>]
    static member inline MapFailure (this: RopResult<'TSuccess, 'TMessage>, f: Func<'TMessage list, 'TMessage2 seq>) =
        this |> Trial.mapFailure (f.Invoke >> Seq.toList)
