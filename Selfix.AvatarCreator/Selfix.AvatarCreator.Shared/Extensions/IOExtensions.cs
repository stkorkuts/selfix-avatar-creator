using LanguageExt;
using LanguageExt.Common;

namespace Selfix.Jobs.Shared.Extensions;

public static class IOExtensions
{
    public static IO<T> WithLogging<T>(this IO<T> io, Action? before, Action? onSuccess, Action<Error>? onError)
    {
        return from _1 in IO<Unit>.Lift(() =>
            {
                before?.Invoke();
                return Unit.Default;
            })
            from result in io.Map(val =>
            {
                onSuccess?.Invoke();
                return val;
            }).MapFail(err =>
            {
                onError?.Invoke(err);
                return err;
            })
            select result;
    }
    
    public static IO<A> TapOnFail<A, B>(this IO<A> io, Func<Error, IO<B>> func) =>
        io.IfFail(error => func(error)
            .Bind(_ => IO.fail<A>(error))
            .IfFail(innerError => IO.fail<A>(innerError + error)));
}