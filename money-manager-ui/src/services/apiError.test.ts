/**
 * The single place that knows what a failed write looks like on the wire.
 *
 * Before this existed, each form reached into `err.response.data` and guessed, which is why the
 * three different error shapes the API used to return went unnoticed: every caller only ever
 * handled the one it happened to meet. Now the server answers RFC 7807 everywhere and this
 * translates it once — so these tests are what stop a form from silently showing nothing when
 * the envelope shifts.
 *
 * The field-name mapping is the part most worth pinning. ASP.NET keys the `errors` map by the
 * property name it validated (`AccountName`), and the forms bind to `accountName`. Get that
 * wrong and the response is perfectly correct while every input stays blank.
 */
import { describe, it, expect } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { extractApiError } from './api';

/** An axios error carrying `data` as the response body, the way the interceptor chain produces. */
function responseWith(status: number, data: unknown): AxiosError {
  const error = new AxiosError('Request failed', undefined, undefined, undefined, {
    status,
    statusText: '',
    data,
    headers: new AxiosHeaders(),
    config: { headers: new AxiosHeaders() },
  });

  return error;
}

describe('validation failures', () => {
  const validationProblem = {
    type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
    title: 'One or more validation errors occurred.',
    status: 400,
    errors: {
      AccountName: ['The AccountName field is required.'],
      Balance: ['Balance cannot be negative.'],
      CurrencyCode: ['CurrencyCode must be one of EUR, HUF, USD, GBP, CHF, PLN, CZK, RON.'],
    },
  };

  it('camel-cases the field names to match the form bindings', () => {
    const { fields } = extractApiError(responseWith(400, validationProblem));

    expect(Object.keys(fields).sort()).toEqual(['accountName', 'balance', 'currencyCode']);
  });

  it('keeps every field that failed, not just the first', () => {
    // The acceptance criterion for this behaviour is a request that breaks three rules at once
    // and names all three, rather than making the user fix them one submit at a time.
    const { fields } = extractApiError(responseWith(400, validationProblem));

    expect(fields.balance).toBe('Balance cannot be negative.');
    expect(fields.currencyCode).toContain('must be one of');
  });

  it('does not surface the generic title when a field message exists', () => {
    // "One or more validation errors occurred" next to three specific inline messages is noise.
    const { message } = extractApiError(responseWith(400, validationProblem));

    expect(message).toBeNull();
  });

  it('takes the first message when a field has several', () => {
    const { fields } = extractApiError(
      responseWith(400, { errors: { Password: ['Too short.', 'Also too obvious.'] } })
    );

    expect(fields.password).toBe('Too short.');
  });
});

describe('failures with no field to blame', () => {
  it('surfaces detail from a conflict', () => {
    // A duplicate rent payment is a 409: nothing about the request is malformed, so there is no
    // input to put a message under and it belongs in a banner instead.
    const { fields, message, status } = extractApiError(
      responseWith(409, { title: 'Conflict', status: 409, detail: 'Rent for 2026-07 is already recorded.' })
    );

    expect(fields).toEqual({});
    expect(message).toBe('Rent for 2026-07 is already recorded.');
    expect(status).toBe(409);
  });

  it('surfaces detail from a server error without inventing a field', () => {
    const { fields, message } = extractApiError(
      responseWith(500, { title: 'An error occurred', status: 500, detail: 'Unexpected.' })
    );

    expect(fields).toEqual({});
    expect(message).toBe('Unexpected.');
  });
});

describe('envelopes this does not control', () => {
  it('survives a response body that is not ProblemDetails', () => {
    // A proxy returning an HTML error page, or anything else that never reached the app. The
    // error handler must not throw its own error.
    const { fields, message } = extractApiError(responseWith(502, '<html>Bad Gateway</html>'));

    expect(fields).toEqual({});
    expect(message).toBeTruthy();
  });

  it('survives a network failure with no response at all', () => {
    const { fields, message, status } = extractApiError(new AxiosError('Network Error'));

    expect(fields).toEqual({});
    expect(message).toBe('Network Error');
    expect(status).toBeNull();
  });

  it('survives something that is not an axios error', () => {
    const { message } = extractApiError(new Error('boom'));

    expect(message).toBe('boom');
  });

  it('survives a thrown value that is not an Error', () => {
    const { fields, message } = extractApiError('just a string');

    expect(fields).toEqual({});
    expect(message).toBe('Something went wrong.');
  });
});
